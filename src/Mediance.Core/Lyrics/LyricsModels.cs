namespace Mediance.Core.Lyrics;

public enum LyricsKind { Synced, Plain, Instrumental, Unavailable }

public sealed record LyricsQuery(string Title, string Artist, string? Album, TimeSpan? Duration);
public sealed record LyricLine(TimeSpan Start, string Text);

public sealed record LyricsDocument(
    LyricsKind Kind,
    IReadOnlyList<LyricLine> Lines,
    string? PlainText = null,
    bool IsUserTimed = false)
{
    public static LyricsDocument Unavailable { get; } = new(LyricsKind.Unavailable, []);
    public static LyricsDocument Instrumental { get; } = new(LyricsKind.Instrumental, []);
}

public interface ILyricsProvider
{
    Task<LyricsDocument> FindAsync(LyricsQuery query, CancellationToken token = default);
}

public interface ILyricsService
{
    Task<LyricsDocument> FindAsync(LyricsQuery query, CancellationToken token = default);
}

public interface IEditableLyricsService : ILyricsService
{
    Task<LyricsDocument> SaveTimingAsync(LyricsQuery query, string plainText,
        IReadOnlyList<TimeSpan> timings, CancellationToken token = default);
}

public static class PlainLyricsTimeline
{
    private static readonly string[] SectionLabels =
    [
        "intro", "giris", "verse", "kita", "nakarat", "chorus", "hook", "pre chorus",
        "on nakarat", "arka nakarat", "ara nakarat", "bridge", "kopru", "outro", "cikis",
        "refrain", "bolum", "instrumental", "enstrumantal"
    ];

    public static string Clean(string? plainText)
    {
        if (string.IsNullOrWhiteSpace(plainText)) return "";
        var lines = plainText.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.Length > 0 && !IsNonLyricLine(line));
        return string.Join('\n', lines);
    }

    private static bool IsNonLyricLine(string line)
    {
        var trimmed = line.Trim();
        var inner = trimmed.Trim('[', ']', '(', ')').Trim();
        if (inner.StartsWith("ar:", StringComparison.OrdinalIgnoreCase) ||
            inner.StartsWith("ti:", StringComparison.OrdinalIgnoreCase) ||
            inner.StartsWith("al:", StringComparison.OrdinalIgnoreCase) ||
            inner.StartsWith("by:", StringComparison.OrdinalIgnoreCase) ||
            inner.StartsWith("offset:", StringComparison.OrdinalIgnoreCase)) return true;

        if (!((trimmed.StartsWith('[') && trimmed.EndsWith(']')) ||
              (trimmed.StartsWith('(') && trimmed.EndsWith(')')))) return false;
        var normalized = NormalizeLabel(inner);
        return SectionLabels.Any(label => normalized == label || normalized.StartsWith(label + " ", StringComparison.Ordinal));
    }

    private static string NormalizeLabel(string value)
    {
        value = value.Replace('ı', 'i').Replace('İ', 'I').Replace('ş', 's').Replace('Ş', 'S')
            .Replace('ğ', 'g').Replace('Ğ', 'G').Replace('ç', 'c').Replace('Ç', 'C')
            .Replace('ö', 'o').Replace('Ö', 'O').Replace('ü', 'u').Replace('Ü', 'U');
        var result = new string(value.ToLowerInvariant().Select(character =>
            char.IsLetterOrDigit(character) ? character : ' ').ToArray());
        return string.Join(' ', result.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
