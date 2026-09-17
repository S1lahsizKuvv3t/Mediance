using System.Diagnostics;
using NAudio.CoreAudioApi;

namespace Mediance.Windows.Audio;

public static class WindowsAudioProcessResolver
{
    public static IReadOnlyList<uint> Find(string sourceAppId, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceAppId);
        using var enumerator = new MMDeviceEnumerator();
        var result = new Dictionary<uint, float>();
        using var collection = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
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
                var peak = 0f;
                try { peak = session.AudioMeterInformation.MasterPeakValue; }
                catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException) { }
                if (!result.TryGetValue(processId, out var current) || peak > current) result[processId] = peak;
            }
        }
        return result.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key)
            .Select(pair => pair.Key).ToArray();
    }

    internal static bool MatchesSource(uint processId, string sourceAppId)
    {
        try
        {
            using var process = Process.GetProcessById(checked((int)processId));
            var expected = Path.GetFileNameWithoutExtension(sourceAppId);
            return string.Equals(process.ProcessName, expected, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(process.ProcessName + ".exe", sourceAppId, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or
                                   System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
