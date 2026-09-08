using System.Text.Json;

namespace Mediance.Core.Settings;

public sealed class UnsupportedSettingsVersionException(int version)
    : Exception($"Unsupported settings version: {version}.");

public sealed class JsonSettingsStore(string path)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private static readonly JsonSerializerOptions Options = new()
    { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    public async Task<WidgetSettings> LoadAsync(CancellationToken token = default)
    {
        await _gate.WaitAsync(token);
        try
        {
            if (!File.Exists(path)) return new();
            try { return Deserialize(await File.ReadAllTextAsync(path, token)); }
            catch (JsonException)
            {
                // Keep the damaged file for diagnosis, then recover the last complete save.
                File.Copy(path, path + ".invalid", true);
                if (File.Exists(path + ".bak"))
                {
                    try
                    {
                        var backupJson = await File.ReadAllTextAsync(path + ".bak", token);
                        var recovered = Deserialize(backupJson);
                        File.Copy(path + ".bak", path, true);
                        return recovered;
                    }
                    catch (JsonException) { }
                }
                return new();
            }
        }
        finally { _gate.Release(); }
    }

    public async Task SaveAsync(WidgetSettings settings, CancellationToken token = default)
    {
        if (settings.SchemaVersion != 1) throw new UnsupportedSettingsVersionException(settings.SchemaVersion);
        await _gate.WaitAsync(token);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            // Preserve future-version files even if another app replaces the file
            // after our initial load. Do not silently downgrade a user's settings.
            if (File.Exists(path))
            {
                try { _ = Deserialize(await File.ReadAllTextAsync(path, token)); }
                catch (JsonException)
                {
                    File.Copy(path, path + ".invalid", true);
                    File.Delete(path);
                }
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(settings.Normalize(), Options), token);
            token.ThrowIfCancellationRequested();
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path);
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            finally { _gate.Release(); }
        }
    }

    private static WidgetSettings Deserialize(string json)
    {
        var settings = JsonSerializer.Deserialize<WidgetSettings>(json, Options)
            ?? throw new JsonException("Settings must be an object.");
        if (settings.SchemaVersion != 1) throw new UnsupportedSettingsVersionException(settings.SchemaVersion);
        return settings.Normalize();
    }
}
