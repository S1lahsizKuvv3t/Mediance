using System.Runtime.InteropServices;
using System.Threading.Channels;
using Mediance.Core.Media;
using Windows.Media.Control;
using Windows.Storage.Streams;
using Session = Windows.Media.Control.GlobalSystemMediaTransportControlsSession;

namespace Mediance.Windows.Media;

/// <summary>Phase 0A adapter. One serialized worker coalesces native notifications.</summary>
public sealed class WindowsMediaSessionService : IMediaSessionService
{
    private sealed record Entry(string Id, Session Native);
    private readonly List<Entry> _entries = [];
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Channel<bool> _changes = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private Task? _worker;
    private Task? _heartbeat;
    private string? _pinnedId;
    private long _nextId;
    private long _revision;
    private MediaSnapshot _snapshot = MediaSnapshot.Empty;
    private bool _started;
    private bool _disposed;

    public MediaSnapshot Snapshot => Volatile.Read(ref _snapshot);
    public event EventHandler<MediaSnapshot>? SnapshotChanged;
    public event EventHandler<string>? Diagnostic;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_started) return;
            _started = true;
        }
        finally { _gate.Release(); }
        _worker = Task.Run(ProcessChangesAsync);
        _heartbeat = Task.Run(HeartbeatAsync);
        try
        {
            await EnsureManagerAsync(cancellationToken);
            await RefreshAsync(cancellationToken);
        }
        catch (Exception ex) when (IsNativeFailure(ex))
        {
            Report(ex);
            Signal();
        }
    }

    private void Signal()
    {
        Interlocked.Increment(ref _revision);
        _changes.Writer.TryWrite(true);
    }
    private void OnSessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args) => Signal();
    private void OnCurrentChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args) => Signal();
    private void OnMediaChanged(Session sender, MediaPropertiesChangedEventArgs args) => Signal();
    private void OnPlaybackChanged(Session sender, PlaybackInfoChangedEventArgs args) => Signal();
    private void OnTimelineChanged(Session sender, TimelinePropertiesChangedEventArgs args) => Signal();

    private async Task ProcessChangesAsync()
    {
        try
        {
            await foreach (var _ in _changes.Reader.ReadAllAsync(_lifetime.Token))
            {
                try
                {
                    await EnsureManagerAsync(_lifetime.Token);
                    await RefreshAsync(_lifetime.Token);
                }
                catch (Exception ex) when (IsNativeFailure(ex))
                {
                    Report(ex);
                    await ResetManagerAsync();
                }
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
    }

    private async Task HeartbeatAsync()
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
            while (await timer.WaitForNextTickAsync(_lifetime.Token)) Signal();
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
    }

    private async Task EnsureManagerAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_manager is not null) return;
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask(cancellationToken);
            _manager.SessionsChanged += OnSessionsChanged;
            _manager.CurrentSessionChanged += OnCurrentChanged;
        }
        finally { _gate.Release(); }
    }

    private async Task ResetManagerAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (_manager is not null)
            {
                _manager.SessionsChanged -= OnSessionsChanged;
                _manager.CurrentSessionChanged -= OnCurrentChanged;
            }
            foreach (var entry in _entries) Unsubscribe(entry.Native);
            _entries.Clear();
            _manager = null;
        }
        finally { _gate.Release(); }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        MediaSnapshot? next = null;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_manager is null) return;
            var revision = Interlocked.Read(ref _revision);
            var nativeSessions = _manager.GetSessions();
            foreach (var removed in _entries.Where(e => !nativeSessions.Any(s => s.Equals(e.Native))).ToArray())
            {
                Unsubscribe(removed.Native);
                _entries.Remove(removed);
            }
            foreach (var native in nativeSessions)
            {
                if (_entries.Any(e => e.Native.Equals(native))) continue;
                _entries.Add(new Entry((++_nextId).ToString(System.Globalization.CultureInfo.InvariantCulture), native));
                native.MediaPropertiesChanged += OnMediaChanged;
                native.PlaybackInfoChanged += OnPlaybackChanged;
                native.TimelinePropertiesChanged += OnTimelineChanged;
            }

            var states = new List<MediaSessionInfo>();
            foreach (var entry in _entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeout.CancelAfter(TimeSpan.FromSeconds(5));
                    var metadata = await entry.Native.TryGetMediaPropertiesAsync().AsTask(timeout.Token);
                    var playback = entry.Native.GetPlaybackInfo();
                    var timeline = entry.Native.GetTimelineProperties();
                    var c = playback.Controls;
                    states.Add(new MediaSessionInfo(entry.Id, entry.Native.SourceAppUserModelId,
                        new(metadata.Title, metadata.Artist, metadata.AlbumTitle), MapStatus(playback.PlaybackStatus),
                        playback.PlaybackRate ?? 1,
                        new(timeline.Position, timeline.StartTime, timeline.EndTime,
                            timeline.MinSeekTime, timeline.MaxSeekTime, timeline.LastUpdatedTime),
                        new(c.IsPlayEnabled, c.IsPauseEnabled, c.IsPlayPauseToggleEnabled,
                            c.IsPreviousEnabled, c.IsNextEnabled, c.IsPlaybackPositionEnabled),
                        metadata.Thumbnail is not null, Kind: metadata.PlaybackType switch
                        {
                            global::Windows.Media.MediaPlaybackType.Music => MediaKind.Music,
                            global::Windows.Media.MediaPlaybackType.Video => MediaKind.Video,
                            global::Windows.Media.MediaPlaybackType.Image => MediaKind.Image,
                            _ => MediaKind.Unknown
                        }));
                }
                catch (Exception ex) when (IsNativeFailure(ex) || ex is OperationCanceledException && !cancellationToken.IsCancellationRequested)
                {
                    Report(ex);
                    // Keep session identity during transient metadata errors so a pin survives.
                    var prior = Snapshot.Sessions.FirstOrDefault(s => s.Id == entry.Id);
                    prior ??= new(entry.Id, "Unavailable", new("", "", ""), PlaybackStatus.Changing, 1,
                        new(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, DateTimeOffset.MinValue),
                        new(false, false, false, false, false, false), false);
                    states.Add(prior with
                    { ReadError = ex.GetType().Name, Capabilities = new(false, false, false, false, false, false) });
                }
            }

            var current = _manager.GetCurrentSession();
            var currentSourceAppId = current?.SourceAppUserModelId;
            var exactCurrentId = _entries.FirstOrDefault(e => e.Native.Equals(current))?.Id;
            var currentId = SessionSelection.ResolveCurrentSessionId(states, exactCurrentId, currentSourceAppId);
            var selection = SessionSelection.Choose(states, currentId, _pinnedId);
            // An event arriving during an async read invalidates this snapshot.
            // The coalesced channel already contains the next refresh request.
            if (revision == Interlocked.Read(ref _revision))
            {
                _pinnedId = selection.PinnedId;
                next = new(states.AsReadOnly(), currentId, _pinnedId, selection.SelectedId, currentSourceAppId);
                Volatile.Write(ref _snapshot, next);
            }
        }
        finally { _gate.Release(); }
        if (next is not null) SnapshotChanged?.Invoke(this, next);
    }

    public async Task PinAsync(string? sessionId, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        MediaSnapshot next;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (sessionId is not null && !_entries.Any(e => e.Id == sessionId))
                throw new ArgumentException("Session no longer exists.", nameof(sessionId));
            _pinnedId = sessionId;
            var selection = SessionSelection.Choose(Snapshot.Sessions, Snapshot.CurrentSessionId, sessionId);
            next = Snapshot with { SelectedSessionId = selection.SelectedId, PinnedSessionId = selection.PinnedId };
            Volatile.Write(ref _snapshot, next);
        }
        finally { _gate.Release(); }
        SnapshotChanged?.Invoke(this, next);
    }

    public async Task<MediaCommandResult> ExecuteAsync(string sessionId, MediaCommand command,
        TimeSpan? position = null, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var session = _entries.FirstOrDefault(e => e.Id == sessionId)?.Native;
            if (session is null) return new(false, "Session no longer exists.");
            var c = session.GetPlaybackInfo().Controls;
            var capabilities = new MediaCapabilities(c.IsPlayEnabled, c.IsPauseEnabled,
                c.IsPlayPauseToggleEnabled, c.IsPreviousEnabled, c.IsNextEnabled, c.IsPlaybackPositionEnabled);
            if (!capabilities.Supports(command)) return new(false, "This source does not support the requested command.");
            if (command == MediaCommand.Seek)
            {
                if (position is null) return new(false, "Seek requires a position.");
                var timeline = session.GetTimelineProperties();
                if (position < timeline.MinSeekTime || position > timeline.MaxSeekTime)
                    return new(false, "Position is outside the source's seek range.");
            }
            var operation = command switch
            {
                MediaCommand.Play => session.TryPlayAsync(),
                MediaCommand.Pause => session.TryPauseAsync(),
                MediaCommand.Toggle => session.TryTogglePlayPauseAsync(),
                MediaCommand.Previous => session.TrySkipPreviousAsync(),
                MediaCommand.Next => session.TrySkipNextAsync(),
                MediaCommand.Seek => session.TryChangePlaybackPositionAsync(position!.Value.Ticks),
                _ => throw new ArgumentOutOfRangeException(nameof(command))
            };
            var accepted = await operation.AsTask(timeout.Token);
            Signal();
            return new(accepted, accepted ? null : "The source rejected the command.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && !_lifetime.IsCancellationRequested)
        { return new(false, "The source did not respond within five seconds."); }
        catch (Exception ex) when (IsNativeFailure(ex))
        { Report(ex); return new(false, $"Windows media error: 0x{ex.HResult:X8}"); }
        finally { _gate.Release(); }
    }

    public async Task<ArtworkData?> ReadArtworkAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var session = _entries.FirstOrDefault(e => e.Id == sessionId)?.Native;
            if (session is null) return null;
            var properties = await session.TryGetMediaPropertiesAsync().AsTask(timeout.Token);
            if (properties.Thumbnail is null) return null;
            using var stream = await properties.Thumbnail.OpenReadAsync().AsTask(timeout.Token);
            const uint maxArtworkBytes = 8 * 1024 * 1024;
            if (stream.Size == 0 || stream.Size > maxArtworkBytes) return null;
            using var reader = new DataReader(stream.GetInputStreamAt(0));
            var loaded = await reader.LoadAsync((uint)stream.Size).AsTask(timeout.Token);
            // Timeline/playback notifications can arrive while the thumbnail stream is read.
            // They do not invalidate artwork; PlayerViewModel's track version cancels truly stale results.
            if (loaded != stream.Size) return null;
            var bytes = new byte[loaded];
            reader.ReadBytes(bytes);
            return new(bytes, stream.ContentType);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && !_lifetime.IsCancellationRequested)
        { Diagnostic?.Invoke(this, "Artwork read timed out."); return null; }
        catch (Exception ex) when (IsNativeFailure(ex)) { Report(ex); return null; }
        finally { _gate.Release(); }
    }

    private static PlaybackStatus MapStatus(GlobalSystemMediaTransportControlsSessionPlaybackStatus status) => status switch
    {
        GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => PlaybackStatus.Playing,
        GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => PlaybackStatus.Paused,
        GlobalSystemMediaTransportControlsSessionPlaybackStatus.Opened => PlaybackStatus.Opened,
        GlobalSystemMediaTransportControlsSessionPlaybackStatus.Changing => PlaybackStatus.Changing,
        GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped => PlaybackStatus.Stopped,
        _ => PlaybackStatus.Closed
    };

    private static bool IsNativeFailure(Exception ex) => ex is COMException or InvalidOperationException or UnauthorizedAccessException;
    private void Report(Exception ex) => Diagnostic?.Invoke(this, $"Media: {ex.GetType().Name} (0x{ex.HResult:X8})");
    private void Unsubscribe(Session session)
    {
        session.MediaPropertiesChanged -= OnMediaChanged;
        session.PlaybackInfoChanged -= OnPlaybackChanged;
        session.TimelinePropertiesChanged -= OnTimelineChanged;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await _lifetime.CancelAsync();
        _changes.Writer.TryComplete();
        if (_worker is not null) await _worker;
        if (_heartbeat is not null) await _heartbeat;
        await _gate.WaitAsync();
        try
        {
            if (_manager is not null)
            {
                _manager.SessionsChanged -= OnSessionsChanged;
                _manager.CurrentSessionChanged -= OnCurrentChanged;
            }
            foreach (var entry in _entries) Unsubscribe(entry.Native);
            _entries.Clear();
            _manager = null;
        }
        finally { _gate.Release(); }
        _lifetime.Dispose();
    }
}
