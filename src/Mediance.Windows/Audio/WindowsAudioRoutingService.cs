using System.Diagnostics;
using Mediance.Core.Audio;
using NAudio.CoreAudioApi;

namespace Mediance.Windows.Audio;

public sealed class WindowsAudioRoutingService : IAudioRoutingService
{
    public Task<AudioRoutingSnapshot> InspectAsync(string sourceAppId, CancellationToken token = default) =>
        Task.Run(() => Inspect(sourceAppId, token), token);

    public Task<AudioRouteResult> SetOutputAsync(string sourceAppId, string? deviceId,
        CancellationToken token = default) => Task.Run(() => SetOutput(sourceAppId, deviceId, token), token);

    public Task<ApplicationVolumeResult> ChangeVolumeAsync(string sourceAppId, float delta,
        CancellationToken token = default) => Task.Run(() => ChangeVolume(sourceAppId, delta, token), token);

    private static AudioRoutingSnapshot Inspect(string sourceAppId, CancellationToken token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceAppId);
        using var enumerator = new MMDeviceEnumerator();
        var devices = ReadDevices(enumerator, token);
        var processIds = ReadMatchingAudioProcesses(enumerator, sourceAppId, token);
        var routes = new List<string?>();
        using var policy = new AudioPolicyConfigAdapter();
        foreach (var processId in processIds)
        {
            token.ThrowIfCancellationRequested();
            routes.Add(policy.GetPersistedRenderEndpoint(processId));
        }

        var distinct = routes.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return new(devices, new(sourceAppId, processIds,
            distinct.Length == 1 ? distinct[0] : null, distinct.Length > 1));
    }

    private static AudioRouteResult SetOutput(string sourceAppId, string? deviceId, CancellationToken token)
    {
        var before = Inspect(sourceAppId, token);
        if (!before.Application.IsAvailable)
            return AudioRouteResult.Failure($"{sourceAppId} için etkin bir ses oturumu bulunamadı.", before);
        if (deviceId is not null && !before.Devices.Any(d =>
                string.Equals(d.Id, deviceId, StringComparison.OrdinalIgnoreCase)))
            return AudioRouteResult.Failure("Seçilen çıkış cihazı artık etkin değil.", before);

        try
        {
            using var policy = new AudioPolicyConfigAdapter();
            foreach (var processId in before.Application.ProcessIds)
            {
                token.ThrowIfCancellationRequested();
                policy.SetPersistedRenderEndpoint(processId, deviceId);
            }

            var after = Inspect(sourceAppId, token);
            var verified = !after.Application.HasMixedRoutes &&
                string.Equals(after.Application.PersistedDeviceId, deviceId, StringComparison.OrdinalIgnoreCase);
            return verified
                ? AudioRouteResult.Success(after)
                : AudioRouteResult.Failure("Windows seçimi kaydettiğini doğrulamadı.", after);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return AudioRouteResult.Failure($"Çıkış değiştirilemedi (0x{ex.HResult:X8}).", Inspect(sourceAppId, token));
        }
    }

    private static ApplicationVolumeResult ChangeVolume(string sourceAppId, float delta, CancellationToken token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceAppId);
        if (!float.IsFinite(delta) || delta == 0)
            return ApplicationVolumeResult.Failure("Ses değişimi geçerli değil.");

        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using var collection = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            float? target = null;
            var matched = 0;
            var allMuted = true;
            for (var deviceIndex = 0; deviceIndex < collection.Count; deviceIndex++)
            {
                token.ThrowIfCancellationRequested();
                using var device = collection[deviceIndex];
                using var manager = device.AudioSessionManager;
                manager.RefreshSessions();
                using var sessions = manager.Sessions;
                for (var sessionIndex = 0; sessionIndex < sessions.Count; sessionIndex++)
                {
                    token.ThrowIfCancellationRequested();
                    using var session = sessions[sessionIndex];
                    if (session.IsSystemSoundsSession) continue;
                    var processId = session.GetProcessID;
                    if (processId == 0 || !MatchesSource(processId, sourceAppId)) continue;
                    using var volume = session.SimpleAudioVolume;
                    target ??= Math.Clamp(volume.Volume + delta, 0, 1);
                    volume.Volume = target.Value;
                    allMuted &= volume.Mute;
                    matched++;
                }
            }

            return matched == 0 || target is null
                ? ApplicationVolumeResult.Failure($"{sourceAppId} için etkin bir ses oturumu bulunamadı.")
                : ApplicationVolumeResult.Success(target.Value, allMuted);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ApplicationVolumeResult.Failure($"Uygulama sesi değiştirilemedi (0x{ex.HResult:X8}).");
        }
    }

    private static IReadOnlyList<AudioOutputDevice> ReadDevices(MMDeviceEnumerator enumerator,
        CancellationToken token)
    {
        string? defaultId = null;
        if (enumerator.TryGetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia, out var defaultDevice))
        {
            using (defaultDevice) defaultId = defaultDevice.ID;
        }

        using var collection = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
        var result = new List<AudioOutputDevice>(collection.Count);
        for (var i = 0; i < collection.Count; i++)
        {
            token.ThrowIfCancellationRequested();
            using var device = collection[i];
            result.Add(new(device.ID, device.FriendlyName,
                string.Equals(device.ID, defaultId, StringComparison.OrdinalIgnoreCase)));
        }
        return result;
    }

    private static IReadOnlyList<uint> ReadMatchingAudioProcesses(MMDeviceEnumerator enumerator,
        string sourceAppId, CancellationToken token)
    {
        var result = new HashSet<uint>();
        using var collection = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
        for (var i = 0; i < collection.Count; i++)
        {
            token.ThrowIfCancellationRequested();
            using var device = collection[i];
            using var manager = device.AudioSessionManager;
            manager.RefreshSessions();
            using var sessions = manager.Sessions;
            for (var sessionIndex = 0; sessionIndex < sessions.Count; sessionIndex++)
            {
                var session = sessions[sessionIndex];
                using (session)
                {
                    if (session.IsSystemSoundsSession) continue;
                    var processId = session.GetProcessID;
                    if (processId != 0 && MatchesSource(processId, sourceAppId)) result.Add(processId);
                }
            }
        }
        return result.Order().ToArray();
    }

    private static bool MatchesSource(uint processId, string sourceAppId)
    {
        try
        {
            using var process = Process.GetProcessById(checked((int)processId));
            var expected = Path.GetFileNameWithoutExtension(sourceAppId);
            return string.Equals(process.ProcessName, expected, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(process.ProcessName + ".exe", sourceAppId, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
