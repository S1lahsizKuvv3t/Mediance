using Mediance.Core.Media;
using Mediance.Core.Lyrics;
using Mediance.Core.Audio;

namespace Mediance.AcrylicProbe;

// Deterministic, original demo content for public screenshots. Never captures
// the user's player, lyrics, artwork or listening state.
internal sealed class PreviewMediaService : IMediaSessionService, ILyricsProvider, IAudioRoutingService
{
    public MediaSnapshot Snapshot { get; private set; } = MediaSnapshot.Empty;
    public event EventHandler<MediaSnapshot>? SnapshotChanged;
    public event EventHandler<string>? Diagnostic { add { } remove { } }
    public Task StartAsync(CancellationToken cancellationToken = default) => RefreshAsync(false, cancellationToken);
    public Task RefreshAsync(bool reconnect = false, CancellationToken cancellationToken = default)
    {
        var session = new MediaSessionInfo("preview", "Demo", new("Midnight Drive", "Mediance", "Demo"),
            PlaybackStatus.Paused, 1, new(TimeSpan.FromSeconds(42), TimeSpan.Zero, TimeSpan.FromSeconds(210),
                TimeSpan.Zero, TimeSpan.FromSeconds(210), DateTimeOffset.UtcNow), new(true, true, true, true, true, true), true);
        Snapshot = new([session], session.Id, null, session.Id);
        SnapshotChanged?.Invoke(this, Snapshot);
        return Task.CompletedTask;
    }
    public Task PinAsync(string? sessionId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<MediaCommandResult> ExecuteAsync(string sessionId, MediaCommand command,
        TimeSpan? position = null, CancellationToken cancellationToken = default) => Task.FromResult(new MediaCommandResult(true));
    public async Task<ArtworkData?> ReadArtworkAsync(string sessionId, CancellationToken cancellationToken = default) =>
        new(await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Assets", "Mediance-mark.png"), cancellationToken), "image/png");
    public Task<LyricsDocument> FindAsync(LyricsQuery query, CancellationToken token = default) =>
        Task.FromResult(new LyricsDocument(LyricsKind.Synced,
            [new(TimeSpan.FromSeconds(10), "City lights pass slowly"),
             new(TimeSpan.FromSeconds(35), "Keep the music close"),
             new(TimeSpan.FromSeconds(65), "Let the night unfold")]));
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    public Task<AudioRoutingSnapshot> InspectAsync(string sourceAppId, CancellationToken token = default) =>
        Task.FromResult(new AudioRoutingSnapshot([new("demo", "Demo headphones", true)],
            new(sourceAppId, [1], null, false)));
    public async Task<AudioRouteResult> SetOutputAsync(string sourceAppId, string? deviceId, CancellationToken token = default) =>
        AudioRouteResult.Success(await InspectAsync(sourceAppId, token));
    public Task<ApplicationVolumeResult> ChangeVolumeAsync(string sourceAppId, float delta, CancellationToken token = default) =>
        Task.FromResult(ApplicationVolumeResult.Success(0.5f, false));
}
