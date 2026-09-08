using System.Globalization;
using System.Text;
using Mediance.Core.Lyrics;

namespace Mediance.Lyrics;

/// <summary>Keeps recent provider results in memory only; no listening metadata is written to disk.</summary>
public sealed class MemoryLyricsCacheProvider : ILyricsProvider
{
    private sealed record Entry(LyricsDocument Document, DateTimeOffset ExpiresUtc);
    private readonly ILyricsProvider _inner;
    private readonly int _capacity;
    private readonly TimeSpan _positiveLifetime;
    private readonly TimeSpan _negativeLifetime;
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly LinkedList<string> _recency = [];
    private readonly object _sync = new();

    public MemoryLyricsCacheProvider(ILyricsProvider inner, int capacity = 96,
        TimeSpan? positiveLifetime = null, TimeSpan? negativeLifetime = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _inner = inner;
        _capacity = capacity;
        _positiveLifetime = positiveLifetime ?? TimeSpan.FromHours(4);
        _negativeLifetime = negativeLifetime ?? TimeSpan.FromSeconds(45);
    }

    public async Task<LyricsDocument> FindAsync(LyricsQuery query, CancellationToken token = default)
    {
        var key = Key(query);
        lock (_sync)
        {
            if (_entries.TryGetValue(key, out var cached))
            {
                if (cached.ExpiresUtc > DateTimeOffset.UtcNow)
                {
                    Touch(key);
                    return cached.Document;
                }
                Remove(key);
            }
        }

        var document = await _inner.FindAsync(query, token);
        token.ThrowIfCancellationRequested();
        var lifetime = document.Kind == LyricsKind.Unavailable ? _negativeLifetime : _positiveLifetime;
        if (lifetime <= TimeSpan.Zero) return document;
        lock (_sync)
        {
            _entries[key] = new(document, DateTimeOffset.UtcNow + lifetime);
            Touch(key);
            while (_entries.Count > _capacity && _recency.First is { } oldest) Remove(oldest.Value);
        }
        return document;
    }

    private void Touch(string key)
    {
        _recency.Remove(key);
        _recency.AddLast(key);
    }

    private void Remove(string key)
    {
        _entries.Remove(key);
        _recency.Remove(key);
    }

    private static string Key(LyricsQuery query)
    {
        static string Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            var normalized = value.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(normalized.Length);
            foreach (var character in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                    builder.Append(char.ToLowerInvariant(character));
            }
            return builder.ToString().Normalize(NormalizationForm.FormC).Trim();
        }

        var duration = query.Duration is { TotalSeconds: > 0 } value
            ? Math.Round(value.TotalSeconds / 2).ToString(CultureInfo.InvariantCulture)
            : "";
        return string.Join('\u001f', Normalize(query.Title), Normalize(query.Artist), Normalize(query.Album), duration);
    }
}
