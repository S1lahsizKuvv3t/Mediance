using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.Runtime.InteropServices;

namespace Mediance.Windows.Audio;

public static class WindowsProcessLoopbackCapture
{
    public static readonly WaveFormat Format = new(16000, 16, 1);

    public static async Task<MemoryStream?> CaptureWaveAsync(
        string sourceAppId, TimeSpan duration, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceAppId);
        if (duration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
        var processes = await Task.Run(() => WindowsAudioProcessResolver.Find(sourceAppId, token), token);
        if (processes.Count == 0) return null;

        using var recorder = await BuildRecorderAsync(processes, token);
        var stream = new MemoryStream();
        try
        {
            using (var writer = new WaveFileWriter(stream, Format))
            using (var captureLimit = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                captureLimit.CancelAfter(duration);
                try
                {
                    await foreach (var buffer in recorder.CaptureAsync(captureLimit.Token))
                        writer.Write(buffer.Data.Span);
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested && captureLimit.IsCancellationRequested) { }
            }
        }
        catch
        {
            stream.Dispose();
            throw;
        }
        stream.Position = 0;
        return stream;
    }

    private static async Task<WasapiRecorder> BuildRecorderAsync(
        IReadOnlyList<uint> processes, CancellationToken token)
    {
        Exception? lastError = null;
        foreach (var processId in processes)
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    return await new WasapiRecorderBuilder()
                        .WithProcessLoopback(processId, ProcessLoopbackMode.IncludeTargetProcessTree)
                        .WithSharedMode()
                        .WithFormat(Format)
                        .WithBufferLength(100)
                        .BuildAsync();
                }
                catch (Exception ex) when (ex is IOException or COMException or InvalidOperationException)
                {
                    lastError = ex;
                    if (attempt == 0) await Task.Delay(300, token);
                }
            }
        }
        throw lastError ?? new InvalidOperationException("No matching audio process could be captured.");
    }
}
