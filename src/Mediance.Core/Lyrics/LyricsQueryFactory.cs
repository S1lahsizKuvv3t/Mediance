namespace Mediance.Core.Lyrics;

public static class LyricsQueryFactory
{
    private static readonly string[] CombinedTitleSeparators = [" - ", " – ", " — "];

    public static LyricsQuery? FromMediaMetadata(string? title, string? artist, string? album,
        TimeSpan? duration)
    {
        title = Clean(title);
        artist = Clean(artist);
        album = Clean(album);
        if (title is null) return null;

        if (artist is not null)
            return new(title, artist, album, duration);

        foreach (var separator in CombinedTitleSeparators)
        {
            var separatorIndex = title.IndexOf(separator, StringComparison.Ordinal);
            if (separatorIndex <= 0) continue;

            var inferredArtist = Clean(title[..separatorIndex]);
            var inferredTitle = Clean(title[(separatorIndex + separator.Length)..]);
            if (inferredArtist is not null && inferredTitle is not null)
                // For episode-style metadata, Album is normally the podcast/feed
                // name rather than the song release and would weaken a valid match.
                return new(inferredTitle, inferredArtist, null, duration);
        }

        // Episode and podcast publishers frequently omit the artist completely.
        // Keep the title searchable; providers that support global search can
        // still find a unique song, while LyricsMatching rejects ambiguous hits.
        return new(title, "", null, duration);
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
