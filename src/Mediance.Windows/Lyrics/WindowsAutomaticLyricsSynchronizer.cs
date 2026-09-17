using Mediance.Core.Lyrics;
using Mediance.Windows.Audio;
using Whisper.net;
using Whisper.net.Ggml;

namespace Mediance.Windows.Lyrics;

public sealed class WindowsAutomaticLyricsSynchronizer(string modelPath) : IAutomaticLyricsSynchronizer
{
    private static readonly TimeSpan MaximumCaptureLength = TimeSpan.FromMinutes(10);
    private readonly SemaphoreSlim _modelGate = new(1, 1);

    public event EventHandler<AutomaticLyricsSyncProgress>? ProgressChanged;

    public async Task<AutomaticLyricsAlignment?> SynchronizeAsync(
        AutomaticLyricsSyncRequest request, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourceAppId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.PlainText);
        var duration = RemainingDuration(request.Query.Duration, request.CaptureOffset);
        if (duration < TimeSpan.FromSeconds(20)) return null;

        ProgressChanged?.Invoke(this, new(AutomaticLyricsSyncStage.Capturing));
        using var audio = await WindowsProcessLoopbackCapture.CaptureWaveAsync(request.SourceAppId, duration, token);
        if (audio is null || audio.Length < WindowsProcessLoopbackCapture.Format.AverageBytesPerSecond * 8L) return null;

        var path = await EnsureModelAsync(token);
        ProgressChanged?.Invoke(this, new(AutomaticLyricsSyncStage.Transcribing));
        var transcript = await TranscribeAsync(audio, path, token);
        if (transcript.Count == 0) return null;

        ProgressChanged?.Invoke(this, new(AutomaticLyricsSyncStage.Aligning));
        return AutomaticLyricsAligner.Align(request.PlainText, transcript,
            request.CaptureOffset, request.Query.Duration);
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
        if (File.Exists(modelPath) && new FileInfo(modelPath).Length > 16 * 1024 * 1024) return modelPath;
        await _modelGate.WaitAsync(token);
        try
        {
            if (File.Exists(modelPath) && new FileInfo(modelPath).Length > 16 * 1024 * 1024) return modelPath;
            ProgressChanged?.Invoke(this, new(AutomaticLyricsSyncStage.DownloadingModel));
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(modelPath))!);
            var temporary = modelPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await using var source = await WhisperGgmlDownloader.Default
                    .GetGgmlModelAsync(GgmlType.Small, cancellationToken: token);
                await using var destination = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                    FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
                await source.CopyToAsync(destination, token);
                await destination.FlushAsync(token);
                token.ThrowIfCancellationRequested();
                File.Move(temporary, modelPath, true);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
            return modelPath;
        }
        finally { _modelGate.Release(); }
    }

    private static async Task<IReadOnlyList<TimedSpeechSegment>> TranscribeAsync(
        Stream audio, string path, CancellationToken token)
    {
        using var factory = WhisperFactory.FromPath(path);
        using var processor = factory.CreateBuilder()
            .WithLanguageDetection()
            .Build();
        var result = new List<TimedSpeechSegment>();
        audio.Position = 0;
        await foreach (var segment in processor.ProcessAsync(audio, token))
        {
            if (!string.IsNullOrWhiteSpace(segment.Text) && segment.End > segment.Start)
                result.Add(new(segment.Start, segment.End, segment.Text));
        }
        return result;
    }
}
