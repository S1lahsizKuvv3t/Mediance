using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mediance.Core.Lyrics;

namespace Mediance.Lyrics;

public sealed class LocalLyricsTimingStore(string path)
{
    private const int Version = 2;
    private const int MaximumEntries = 500;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public async Task<IReadOnlyList<TimeSpan>?> LoadAsync(
        LyricsQuery query, string plainText, CancellationToken token = default)
    {
        var identity = Identity.Create(query, plainText);
        await _gate.WaitAsync(token);
        try
        {
            var data = await ReadAsync(token);
            var candidates = data.Entries.Where(entry =>
                entry.TrackKey == identity.TrackKey &&
                entry.TimingsMilliseconds.Length == identity.LineKeys.Length &&
                IsDurationCompatible(entry.DurationMilliseconds, identity.DurationMilliseconds) &&
                IsValid(entry.TimingsMilliseconds, identity.DurationMilliseconds)).ToArray();
            var match = candidates.FirstOrDefault(entry => entry.LyricsKey == identity.LyricsKey) ??
                candidates.OrderByDescending(entry => LineSimilarity(entry.LineKeys, identity.LineKeys))
                    .FirstOrDefault(entry => LineSimilarity(entry.LineKeys, identity.LineKeys) >= 0.85);
            return match?.TimingsMilliseconds.Select(value => TimeSpan.FromMilliseconds(value)).ToArray();
        }
        finally { _gate.Release(); }
    }

    public async Task SaveAsync(LyricsQuery query, string plainText,
        IReadOnlyList<TimeSpan> timings, CancellationToken token = default)
    {
        var identity = Identity.Create(query, plainText);
        var milliseconds = timings.Select(value => (long)Math.Round(value.TotalMilliseconds)).ToArray();
        if (milliseconds.Length != identity.LineKeys.Length || !IsValid(milliseconds, identity.DurationMilliseconds))
            throw new ArgumentException("Lyrics timings must be increasing and match every lyric line.", nameof(timings));

        await _gate.WaitAsync(token);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var data = await ReadAsync(token);
            data.Entries.RemoveAll(entry =>
                entry.TrackKey == identity.TrackKey && entry.LyricsKey == identity.LyricsKey);
            data.Entries.Add(new(identity.TrackKey, identity.LyricsKey, identity.LineKeys,
                identity.DurationMilliseconds, milliseconds, DateTimeOffset.UtcNow));
            data.Entries = data.Entries.OrderByDescending(entry => entry.UpdatedUtc)
                .Take(MaximumEntries).ToList();

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(data, Options), token);
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

    private async Task<TimingFile> ReadAsync(CancellationToken token)
    {
        if (!File.Exists(path)) return new();
        try
        {
            return Deserialize(await File.ReadAllTextAsync(path, token));
        }
        catch (JsonException)
        {
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

    private static TimingFile Deserialize(string json)
    {
        var data = JsonSerializer.Deserialize<TimingFile>(json, Options)
            ?? throw new JsonException("Lyrics timing data must be an object.");
        if (data.SchemaVersion != Version) return new();
        data.Entries ??= [];
        data.Entries = data.Entries.Where(entry =>
            !string.IsNullOrWhiteSpace(entry.TrackKey) &&
            !string.IsNullOrWhiteSpace(entry.LyricsKey) &&
            entry.LineKeys is not null &&
            entry.TimingsMilliseconds is not null &&
            entry.LineKeys.Length == entry.TimingsMilliseconds.Length &&
            IsValid(entry.TimingsMilliseconds, entry.DurationMilliseconds)).ToList();
        return data;
    }

    private static bool IsDurationCompatible(long? saved, long? current) =>
        saved is null || current is null || Math.Abs(saved.Value - current.Value) <= 20_000;

    private static bool IsValid(IReadOnlyList<long> values, long? duration)
    {
        if (values.Count == 0) return false;
        long previous = -1;
        foreach (var value in values)
        {
            if (value < 0 || value <= previous) return false;
            previous = value;
        }
        return duration is null || previous <= duration.Value + 10_000;
    }

    private static double LineSimilarity(IReadOnlyList<string> saved, IReadOnlyList<string> current)
    {
        if (saved.Count == 0 || saved.Count != current.Count) return 0;
        var matches = saved.Zip(current).Count(pair => pair.First == pair.Second);
        return (double)matches / saved.Count;
    }

    private sealed class TimingFile
    {
        public int SchemaVersion { get; init; } = Version;
        public List<TimingEntry> Entries { get; set; } = [];
    }

    private sealed record TimingEntry(
        string TrackKey,
        string LyricsKey,
        string[] LineKeys,
        long? DurationMilliseconds,
        long[] TimingsMilliseconds,
        DateTimeOffset UpdatedUtc);

    private sealed record Identity(string TrackKey, string LyricsKey, string[] LineKeys, long? DurationMilliseconds)
    {
        public static Identity Create(LyricsQuery query, string plainText)
        {
            var lines = PlainLyricsTimeline.Clean(plainText).Split('\n',
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var lineKeys = lines.Select(line => Hash(Normalize(line), 16)).ToArray();
            var track = Normalize(query.Title) + "|" + Normalize(query.Artist);
            var lyrics = string.Join('|', lines.Select(Normalize));
            var duration = query.Duration is { TotalMilliseconds: > 0 and <= 3_600_000 } value
                ? (long?)Math.Round(value.TotalMilliseconds)
                : null;
            return new(Hash(track), Hash(lyrics), lineKeys, duration);
        }

        private static string Normalize(string value)
        {
            var normalized = value.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(normalized.Length);
            var space = true;
            foreach (var character in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(char.ToLowerInvariant(character));
                    space = false;
                }
                else if (!space)
                {
                    builder.Append(' ');
                    space = true;
                }
            }
            return builder.ToString().Trim();
        }

        private static string Hash(string value, int length = 64)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
            return Convert.ToHexString(bytes)[..length];
        }
    }
}
