namespace Mediance.Core.Media;

public static class SessionSelection
{
    // GetCurrentSession can return a different native object from GetSessions.
    // A unique source match is safe; never collapse multiple browser sessions
    // merely because they share an application ID.
    public static string? ResolveCurrentSessionId(
        IReadOnlyList<MediaSessionInfo> sessions, string? exactSessionId, string? currentSourceAppId)
    {
        if (exactSessionId is not null && sessions.Any(s => s.Id == exactSessionId)) return exactSessionId;
        if (string.IsNullOrWhiteSpace(currentSourceAppId)) return null;
        string? match = null;
        foreach (var session in sessions)
        {
            if (!string.Equals(session.SourceAppId, currentSourceAppId, StringComparison.Ordinal)) continue;
            if (match is not null) return null;
            match = session.Id;
        }
        return match;
    }

    // An explicit pin wins. Otherwise Spotify is preferred while its session
    // exists, followed by a browser music session (YouTube Music). Normal
    // browser video never receives that preference. If neither is available,
    // follow Windows current/most-recent behavior.
    public static (string? SelectedId, string? PinnedId) Choose(
        IReadOnlyList<MediaSessionInfo> sessions, string? currentId, string? pinnedId)
    {
        if (pinnedId is not null && sessions.Any(s => s.Id == pinnedId))
            return (pinnedId, pinnedId);
        var spotify = Best(sessions.Where(IsSpotify), currentId);
        if (spotify is not null) return (spotify.Id, null);
        var youtubeMusic = Best(sessions.Where(IsYouTubeMusic), currentId);
        if (youtubeMusic is not null) return (youtubeMusic.Id, null);
        if (currentId is not null && sessions.Any(s => s.Id == currentId))
            return (currentId, null);
        return (sessions.FirstOrDefault(s => s.Status == PlaybackStatus.Playing)?.Id
            ?? sessions.FirstOrDefault()?.Id, null);
    }

    private static MediaSessionInfo? Best(IEnumerable<MediaSessionInfo> candidates, string? currentId)
    {
        var matches = candidates.ToArray();
        return matches.FirstOrDefault(s => s.Id == currentId)
            ?? matches.FirstOrDefault(s => s.Status == PlaybackStatus.Playing)
            ?? matches.FirstOrDefault();
    }

    private static bool IsSpotify(MediaSessionInfo session) =>
        session.SourceAppId.Contains("spotify", StringComparison.OrdinalIgnoreCase);

    private static bool IsYouTubeMusic(MediaSessionInfo session)
    {
        var source = session.SourceAppId;
        if (source.Contains("youtubemusic", StringComparison.OrdinalIgnoreCase) ||
            source.Contains("youtube music", StringComparison.OrdinalIgnoreCase) ||
            source.Contains("ytmusic", StringComparison.OrdinalIgnoreCase)) return true;
        return session.Kind == MediaKind.Music && IsBrowser(source);
    }

    private static bool IsBrowser(string source) =>
        source.Contains("chrome", StringComparison.OrdinalIgnoreCase) ||
        source.Contains("msedge", StringComparison.OrdinalIgnoreCase) ||
        source.Contains("opera", StringComparison.OrdinalIgnoreCase) ||
        source.Contains("brave", StringComparison.OrdinalIgnoreCase) ||
        source.Contains("firefox", StringComparison.OrdinalIgnoreCase);
}
