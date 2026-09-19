using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mediance.Core.Lyrics;

namespace Mediance.Lyrics;

public sealed record AutomaticLyricsLearningSnapshot(
    int FailureCount,
    DateTimeOffset? RetryAfterUtc,
    string LastReason,
    IReadOnlyList<AutomaticLyricsAnchor> Anchors);

public sealed class AutomaticLyricsLearningStore(string path)
{
    private const int Version = 1;
    private const int MaximumEntries = 300;
    private const int MaximumSamplesPerLine = 5;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public async Task<AutomaticLyricsLearningSnapshot> LoadAsync(
        LyricsQuery query, string plainText, CancellationToken token = default)
    {
        var key = CreateKey(query, plainText);
        await _gate.WaitAsync(token);
        try
        {
            var entry = (await ReadAsync(token)).Entries.FirstOrDefault(value => value.Key == key);
            return entry is null ? new(0, null, "", []) : ToSnapshot(entry);
        }
        finally { _gate.Release(); }
    }

    public async Task<AutomaticLyricsLearningSnapshot> RecordAsync(
        LyricsQuery query,
        string plainText,
        string reason,
        IReadOnlyList<AutomaticLyricsAnchor> anchors,
        bool failed,
        CancellationToken token = default)
    {
        var key = CreateKey(query, plainText);
        await _gate.WaitAsync(token);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var data = await ReadAsync(token);
            var entry = data.Entries.FirstOrDefault(value => value.Key == key) ?? new LearningEntry { Key = key };
            data.Entries.RemoveAll(value => value.Key == key);
            foreach (var anchor in anchors.Where(value => value.LineIndex >= 0 && value.Start >= TimeSpan.Zero))
            {
                var sample = entry.Anchors.FirstOrDefault(value => value.LineIndex == anchor.LineIndex);
                if (sample is null)
                {
                    sample = new AnchorSamples { LineIndex = anchor.LineIndex };
                    entry.Anchors.Add(sample);
                }
                sample.ValuesMilliseconds.Add((long)Math.Round(anchor.Start.TotalMilliseconds));
                sample.ValuesMilliseconds = sample.ValuesMilliseconds
                    .Distinct().OrderByDescending(value => value).Take(MaximumSamplesPerLine).Order().ToList();
            }
            entry.FailureCount = failed ? Math.Min(99, entry.FailureCount + 1) : 0;
            entry.LastReason = SanitizeReason(reason);
            entry.UpdatedUtc = DateTimeOffset.UtcNow;
            entry.RetryAfterUtc = failed && entry.FailureCount >= 3
                ? DateTimeOffset.UtcNow + TimeSpan.FromMinutes(30)
                : null;
            data.Entries.Add(entry);
            data.Entries = data.Entries.OrderByDescending(value => value.UpdatedUtc)
                .Take(MaximumEntries).ToList();
            await WriteAsync(data, temporary, token);
            return ToSnapshot(entry);
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            finally { _gate.Release(); }
        }
    }

    public async Task ClearAsync(LyricsQuery query, string plainText, CancellationToken token = default)
    {
        var key = CreateKey(query, plainText);
        await _gate.WaitAsync(token);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var data = await ReadAsync(token);
            if (data.Entries.RemoveAll(value => value.Key == key) > 0)
                await WriteAsync(data, temporary, token);
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            finally { _gate.Release(); }
        }
    }

    private static AutomaticLyricsLearningSnapshot ToSnapshot(LearningEntry entry)
    {
        var anchors = entry.Anchors
            .Where(value => value.ValuesMilliseconds.Count > 0)
            .Select(value =>
            {
                var ordered = value.ValuesMilliseconds.Order().ToArray();
                return new AutomaticLyricsAnchor(value.LineIndex,
                    TimeSpan.FromMilliseconds(ordered[ordered.Length / 2]));
            })
            .OrderBy(value => value.LineIndex)
            .ToArray();
        return new(entry.FailureCount, entry.RetryAfterUtc, entry.LastReason, anchors);
    }

    private async Task WriteAsync(LearningFile data, string temporary, CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(data, Options), token);
        token.ThrowIfCancellationRequested();
        if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
        else File.Move(temporary, path);
    }

    private async Task<LearningFile> ReadAsync(CancellationToken token)
    {
        if (!File.Exists(path)) return new();
        try
        {
            var data = JsonSerializer.Deserialize<LearningFile>(
                await File.ReadAllTextAsync(path, token), Options) ?? new();
            if (data.SchemaVersion != Version) return new();
            data.Entries ??= [];
            data.Entries = data.Entries.Where(value =>
                value is not null && value.Key is { Length: 64 } && value.Anchors is not null).ToList();
            return data;
        }
        catch (JsonException)
        {
            File.Copy(path, path + ".invalid", true);
            return new();
        }
    }

    private static string CreateKey(LyricsQuery query, string plainText)
    {
        var value = string.Join('|', Normalize(query.Title), Normalize(query.Artist),
            Math.Round(query.Duration?.TotalSeconds ?? 0), Normalize(PlainLyricsTimeline.Clean(plainText)));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private static string Normalize(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark &&
                char.IsLetterOrDigit(character))
                result.Append(char.ToLowerInvariant(character));
        return result.ToString();
    }

    private static string SanitizeReason(string value) => value switch
    {
        "empty" or "low-confidence" or "capture-error" or "cancelled" => value,
        _ => "unknown"
    };

    private sealed class LearningFile
    {
        public int SchemaVersion { get; init; } = Version;
        public List<LearningEntry> Entries { get; set; } = [];
    }

    private sealed class LearningEntry
    {
        public string Key { get; set; } = "";
        public int FailureCount { get; set; }
        public DateTimeOffset? RetryAfterUtc { get; set; }
        public string LastReason { get; set; } = "";
        public List<AnchorSamples> Anchors { get; set; } = [];
        public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
    }

    private sealed class AnchorSamples
    {
        public int LineIndex { get; set; }
        public List<long> ValuesMilliseconds { get; set; } = [];
    }
}
