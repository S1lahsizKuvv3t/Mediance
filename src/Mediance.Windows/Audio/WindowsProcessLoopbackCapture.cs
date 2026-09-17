using NAudio.CoreAudioApi;
using NAudio.Wave;

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

        using var recorder = await new WasapiRecorderBuilder()
            .WithProcessLoopback(processes[0], ProcessLoopbackMode.IncludeTargetProcessTree)
            .WithSharedMode()
            .WithFormat(Format)
            .WithBufferLength(100)
            .BuildAsync();
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
}
