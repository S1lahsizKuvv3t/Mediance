namespace Mediance.Core.Audio;

public sealed record AudioOutputDevice(string Id, string Name, bool IsSystemDefault);

public sealed record ApplicationAudioRoute(
    string SourceAppId,
    IReadOnlyList<uint> ProcessIds,
    string? PersistedDeviceId,
    bool HasMixedRoutes)
{
    public bool IsAvailable => ProcessIds.Count > 0;
}

public sealed record AudioRoutingSnapshot(
    IReadOnlyList<AudioOutputDevice> Devices,
    ApplicationAudioRoute Application);

public sealed record AudioRouteResult(bool Succeeded, string? Error, AudioRoutingSnapshot Snapshot)
{
    public static AudioRouteResult Success(AudioRoutingSnapshot snapshot) => new(true, null, snapshot);
    public static AudioRouteResult Failure(string error, AudioRoutingSnapshot snapshot) => new(false, error, snapshot);
}

public sealed record ApplicationVolumeResult(bool Succeeded, float Volume, bool IsMuted, string? Error)
{
    public int Percent => (int)Math.Round(Math.Clamp(Volume, 0, 1) * 100);
    public static ApplicationVolumeResult Success(float volume, bool isMuted) =>
        new(true, Math.Clamp(volume, 0, 1), isMuted, null);
    public static ApplicationVolumeResult Failure(string error) => new(false, 0, false, error);
}

public interface IAudioRoutingService
{
    Task<AudioRoutingSnapshot> InspectAsync(string sourceAppId, CancellationToken token = default);
    Task<AudioRouteResult> SetOutputAsync(string sourceAppId, string? deviceId, CancellationToken token = default);
    Task<ApplicationVolumeResult> ChangeVolumeAsync(string sourceAppId, float delta,
        CancellationToken token = default);
}
