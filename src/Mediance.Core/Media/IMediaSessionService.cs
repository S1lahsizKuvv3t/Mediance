namespace Mediance.Core.Media;

public interface IMediaSessionService : IAsyncDisposable
{
    MediaSnapshot Snapshot { get; }
    event EventHandler<MediaSnapshot>? SnapshotChanged;
    event EventHandler<string>? Diagnostic;
    Task StartAsync(CancellationToken cancellationToken = default);
    Task PinAsync(string? sessionId, CancellationToken cancellationToken = default);
    Task<MediaCommandResult> ExecuteAsync(string sessionId, MediaCommand command,
        TimeSpan? position = null, CancellationToken cancellationToken = default);
    Task<ArtworkData?> ReadArtworkAsync(string sessionId, CancellationToken cancellationToken = default);
}
