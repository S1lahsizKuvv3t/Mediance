using Mediance.Core.Lyrics;
using Mediance.Windows.Audio;
using System.Diagnostics;
using Whisper.net;
using Whisper.net.Ggml;

namespace Mediance.Windows.Lyrics;

public sealed class WindowsAutomaticLyricsSynchronizer(string modelPath) : IAutomaticLyricsSynchronizer
{
    private static readonly TimeSpan MaximumCaptureLength = TimeSpan.FromSeconds(75);
    private const long MinimumValidModelBytes = 400_000_000;
    private const long ExpectedSmallModelBytes = 487_601_967;
    private readonly SemaphoreSlim _modelGate = new(1, 1);

    public event EventHandler<AutomaticLyricsSyncProgress>? ProgressChanged;

    public async Task<AutomaticLyricsAlignment?> SynchronizeAsync(
        AutomaticLyricsSyncRequest request, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourceAppId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.PlainText);
        var preparationStarted = Stopwatch.GetTimestamp();
        var path = await EnsureModelAsync(token);
        var captureOffset = request.CaptureOffset + Stopwatch.GetElapsedTime(preparationStarted);
        var duration = RemainingDuration(request.Query.Duration, captureOffset);
        if (duration < TimeSpan.FromSeconds(20)) return null;

        ProgressChanged?.Invoke(this, new(AutomaticLyricsSyncStage.Capturing, 0));
        var captureProgress = new Progress<double>(fraction =>
            ProgressChanged?.Invoke(this, new(AutomaticLyricsSyncStage.Capturing, fraction)));
        using var audio = await WindowsProcessLoopbackCapture.CaptureWaveAsync(
            request.SourceAppId, duration, token, captureProgress);
        if (audio is null || audio.Length < WindowsProcessLoopbackCapture.Format.AverageBytesPerSecond * 8L) return null;

        ProgressChanged?.Invoke(this, new(AutomaticLyricsSyncStage.Transcribing));
        var transcript = await TranscribeAsync(audio, path, request, captureOffset, token);
        if (transcript.Count == 0) return null;

        ProgressChanged?.Invoke(this, new(AutomaticLyricsSyncStage.Aligning));
        return AutomaticLyricsAligner.Align(request.PlainText, transcript,
            captureOffset, request.Query.Duration);
    }

    private static TimeSpan RemainingDuration(TimeSpan? duration, TimeSpan offset)
    {
        var remaining = duration is { TotalSeconds: > 0 } value
            ? value - offset - TimeSpan.FromSeconds(1)
            : TimeSpan.FromMinutes(6);
        return remaining > MaximumCaptureLength ? MaximumCaptureLength : remaining;
    }

    private async Task<string> EnsureModelAsync(CancellationToken token)
    {
        if (IsValidModel(modelPath)) return modelPath;
        await _modelGate.WaitAsync(token);
        try
        {
            if (IsValidModel(modelPath)) return modelPath;
            if (File.Exists(modelPath)) File.Delete(modelPath);
            ProgressChanged?.Invoke(this, new(AutomaticLyricsSyncStage.DownloadingModel));
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(modelPath))!);
            var temporary = modelPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await using (var source = await WhisperGgmlDownloader.Default
                    .GetGgmlModelAsync(GgmlType.Small, cancellationToken: token))
                await using (var destination = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                    FileShare.None, 128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    var buffer = new byte[128 * 1024];
                    long copied = 0;
                    var expected = source.CanSeek && source.Length > 0 ? source.Length : ExpectedSmallModelBytes;
                    double lastReported = -1;
                    int read;
                    while ((read = await source.ReadAsync(buffer, token)) > 0)
                    {
                        await destination.WriteAsync(buffer.AsMemory(0, read), token);
                        copied += read;
                        var fraction = Math.Clamp((double)copied / expected, 0, 0.99);
                        if (fraction - lastReported >= 0.005)
                        {
                            lastReported = fraction;
                            ProgressChanged?.Invoke(this,
                                new(AutomaticLyricsSyncStage.DownloadingModel, fraction));
                        }
                    }
                    await destination.FlushAsync(token);
                }
                token.ThrowIfCancellationRequested();
                if (new FileInfo(temporary).Length < MinimumValidModelBytes)
                    throw new InvalidDataException("The downloaded speech model is incomplete.");
                if (!IsValidModel(modelPath)) File.Move(temporary, modelPath, true);
                ProgressChanged?.Invoke(this, new(AutomaticLyricsSyncStage.DownloadingModel, 1));
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
            return modelPath;
        }
        finally { _modelGate.Release(); }
    }

    private static bool IsValidModel(string path) =>
        File.Exists(path) && new FileInfo(path).Length >= MinimumValidModelBytes;

    private static async Task<IReadOnlyList<TimedSpeechSegment>> TranscribeAsync(
        Stream audio, string path, AutomaticLyricsSyncRequest request, TimeSpan captureOffset,
        CancellationToken token)
    {
        using var factory = WhisperFactory.FromPath(path);
        var builder = factory.CreateBuilder()
            .WithLanguageDetection()
            .WithThreads(Math.Clamp(Environment.ProcessorCount - 2, 2, 8))
            .WithMaxSegmentLength(48)
            .SplitOnWord();
        var prompt = BuildPrompt(request, captureOffset);
        if (prompt.Length > 0) builder.WithPrompt(prompt);
        using var processor = builder.Build();
        var result = new List<TimedSpeechSegment>();
        audio.Position = 0;
        await foreach (var segment in processor.ProcessAsync(audio, token))
        {
            if (!string.IsNullOrWhiteSpace(segment.Text) && segment.End > segment.Start)
                result.Add(new(segment.Start, segment.End, segment.Text));
        }
        return result;
    }

    private static string BuildPrompt(AutomaticLyricsSyncRequest request, TimeSpan captureOffset)
    {
        var lines = PlainLyricsTimeline.Clean(request.PlainText).Split('\n',
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0) return "";
        var ratio = request.Query.Duration is { TotalSeconds: > 0 } duration
            ? Math.Clamp(captureOffset.TotalSeconds / duration.TotalSeconds, 0, 1)
            : 0;
        var center = Math.Clamp((int)Math.Round(ratio * (lines.Length - 1)), 0, lines.Length - 1);
        var start = Math.Max(0, center - 8);
        var excerpt = string.Join(' ', lines.Skip(start).Take(18));
        if (excerpt.Length > 900) excerpt = excerpt[..900];
        return $"{request.Query.Artist}. {request.Query.Title}. {excerpt}";
    }
}
