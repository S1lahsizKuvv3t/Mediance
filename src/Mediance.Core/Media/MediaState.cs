namespace Mediance.Core.Media;

public enum PlaybackStatus { Closed, Opened, Changing, Stopped, Playing, Paused }
public enum MediaCommand { Play, Pause, Toggle, Previous, Next, Seek }
public enum MediaKind { Unknown, Music, Video, Image }

public sealed record TrackMetadata(string Title, string Artist, string Album);

public sealed record MediaCapabilities(
    bool CanPlay, bool CanPause, bool CanToggle, bool CanPrevious, bool CanNext, bool CanSeek)
{
    public bool Supports(MediaCommand command) => command switch
    {
        MediaCommand.Play => CanPlay,
        MediaCommand.Pause => CanPause,
        MediaCommand.Toggle => CanToggle,
        MediaCommand.Previous => CanPrevious,
        MediaCommand.Next => CanNext,
        MediaCommand.Seek => CanSeek,
        _ => false
    };
}

public sealed record PlaybackTimeline(
    TimeSpan Position, TimeSpan Start, TimeSpan End,
    TimeSpan MinSeek, TimeSpan MaxSeek, DateTimeOffset LastUpdated);

public sealed record MediaSessionInfo(
    string Id, string SourceAppId, TrackMetadata Track,
    PlaybackStatus Status, double PlaybackRate,
    PlaybackTimeline Timeline, MediaCapabilities Capabilities,
    bool HasArtwork, string? ReadError = null, MediaKind Kind = MediaKind.Unknown);

public sealed record MediaSnapshot(
    IReadOnlyList<MediaSessionInfo> Sessions, string? CurrentSessionId,
    string? PinnedSessionId, string? SelectedSessionId, string? CurrentSourceAppId = null)
{
    public static MediaSnapshot Empty { get; } = new([], null, null, null);
    public MediaSessionInfo? Selected => Sessions.FirstOrDefault(s => s.Id == SelectedSessionId);
}

public sealed record MediaCommandResult(bool Succeeded, string? Error = null);
public sealed record ArtworkData(byte[] Bytes, string ContentType);
