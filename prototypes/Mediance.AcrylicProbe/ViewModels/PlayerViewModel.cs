using System.ComponentModel;
using Mediance.AcrylicProbe.Localization;
using Mediance.Core.Media;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;
using Windows.UI;

namespace Mediance.AcrylicProbe.ViewModels;

public sealed class PlayerViewModel : INotifyPropertyChanged, IAsyncDisposable
{
    private readonly IMediaSessionService _media;
    private readonly IArtworkPaletteService _palette;
    private readonly DispatcherQueue _dispatcher;
    private readonly DispatcherQueueTimer _timelineTimer;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly TimelineClock _timeline = new();
    private CancellationTokenSource? _artworkCancellation;
    private Task? _artworkLoadTask;
    private MediaSessionInfo? _selected;
    private (string Id, TrackMetadata Track)? _artworkKey;
    private bool _busy;
    private bool _disposed;
    private int _artworkVersion;
    private DateTimeOffset _lastArtworkAttempt;
    private Task? _startTask;
    private ImageSource? _artwork;
    private Color _ambientColor = Color.FromArgb(255, 48, 82, 116);
    private string _error = "";

    public PlayerViewModel(IMediaSessionService media, IArtworkPaletteService palette, DispatcherQueue dispatcher)
    {
        _media = media;
        _palette = palette;
        _dispatcher = dispatcher;
        _media.SnapshotChanged += OnSnapshotChanged;
        _media.Diagnostic += OnDiagnostic;
        _timelineTimer = dispatcher.CreateTimer();
        _timelineTimer.Interval = TimeSpan.FromMilliseconds(250);
        _timelineTimer.IsRepeating = true;
        _timelineTimer.Tick += TimelineTimer_Tick;
        _timelineTimer.Start();
    }

    public string Title => _selected is null ? TextCatalog.Get("IdleTitle") :
        string.IsNullOrWhiteSpace(_selected.Track.Title) ? TextCatalog.Get("Untitled") : _selected.Track.Title;
    public string Artist => _selected is null ? TextCatalog.Get("IdleArtist") : _selected.Track.Artist;
    public string Source => _selected is null ? TextCatalog.Get("IdleSource") :
        _selected.SourceAppId.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? _selected.SourceAppId[..^4] : _selected.SourceAppId;
    public string? SourceAppId => _selected?.SourceAppId;
    public Mediance.Core.Lyrics.LyricsQuery? LyricsQuery => _selected is null ||
        string.IsNullOrWhiteSpace(_selected.Track.Title) || string.IsNullOrWhiteSpace(_selected.Track.Artist)
        ? null
        : new(_selected.Track.Title, _selected.Track.Artist, _selected.Track.Album,
            _selected.Timeline.End > TimeSpan.Zero ? _selected.Timeline.End : null);
    public string? TrackIdentity => _selected is null ? null :
        $"{_selected.SourceAppId}\u001f{_selected.Track.Title}\u001f{_selected.Track.Artist}\u001f{_selected.Track.Album}";
    public TimeSpan EstimatedPosition => _timeline.Position;
    public double Progress => _selected is null ? 0 : PlaybackProgress.Fraction(
        EstimatedPosition, _selected.Timeline.Start, _selected.Timeline.End);
    public string PositionText => _selected is null ? "0:00" : FormatTime(EstimatedPosition);
    public string DurationText => _selected is null ? "0:00" : FormatTime(_selected.Timeline.End);
    public string ProgressLabel => _selected is null ? "" :
        $"{PositionText} / {DurationText}";
    public Visibility ProgressVisibility => _selected is not null &&
        _selected.Timeline.End > _selected.Timeline.Start ? Visibility.Visible : Visibility.Collapsed;
    public string Status => _selected is null ? "" : TextCatalog.Get(_selected.Status == PlaybackStatus.Playing ? "Playing" : "Paused");
    public ImageSource? Artwork => _artwork;
    public Color AmbientColor => _ambientColor;
    public string Error => _error;
    public bool CanPrevious => !_busy && _selected?.Capabilities.CanPrevious == true;
    public bool CanNext => !_busy && _selected?.Capabilities.CanNext == true;
    public bool CanToggle => !_busy && _selected is not null &&
        (_selected.Capabilities.CanToggle || (_selected.Status == PlaybackStatus.Playing ? _selected.Capabilities.CanPause : _selected.Capabilities.CanPlay));
    public bool CanSeek => !_busy && _selected?.Capabilities.CanSeek == true &&
        _selected.Timeline.End > _selected.Timeline.Start;
    public bool IsPlaying => _selected?.Status == PlaybackStatus.Playing;
    public string PlayGlyph => _selected?.Status == PlaybackStatus.Playing ? "\uE769" : "\uE768";
    public string PlayLabel => _selected?.Status == PlaybackStatus.Playing ? TextCatalog.Pause : TextCatalog.Play;
    public event PropertyChangedEventHandler? PropertyChanged;

    public Task StartAsync() => _startTask ??= InitializeAsync();
    private async Task InitializeAsync()
    {
        try { await Task.Run(() => _media.StartAsync(_lifetime.Token), _lifetime.Token); }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception ex) { ProbeLog.Write("MediaStart", ex); _error = TextCatalog.Get("MediaError"); Raise(); }
    }

    private void OnSnapshotChanged(object? sender, MediaSnapshot snapshot) =>
        _dispatcher.TryEnqueue(() => { if (!_disposed) Apply(snapshot.Selected); });
    private void OnDiagnostic(object? sender, string diagnostic) => ProbeLog.Write(diagnostic);

    private void Apply(MediaSessionInfo? selected)
    {
        _selected = selected;
        if (selected is not null) _timeline.Reconcile(selected.Timeline,
            selected.Status == PlaybackStatus.Playing, selected.PlaybackRate);
        (string Id, TrackMetadata Track)? key = selected is null ? null : (selected.Id, selected.Track);
        if (_artworkKey != key)
        {
            _artworkKey = key;
            _artworkCancellation?.Cancel();
            _artworkCancellation?.Dispose();
            _artworkCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            var version = ++_artworkVersion;
            _artwork = null;
            _ambientColor = Color.FromArgb(255, 48, 82, 116);
            _error = "";
            if (selected?.HasArtwork == true) StartArtworkLoad(selected.Id, version, _artworkCancellation.Token);
        }
        else if (selected?.HasArtwork == true && _artwork is null &&
            (_artworkLoadTask is null || _artworkLoadTask.IsCompleted) &&
            DateTimeOffset.UtcNow - _lastArtworkAttempt >= TimeSpan.FromSeconds(5))
            StartArtworkLoad(selected.Id, _artworkVersion, _artworkCancellation?.Token ?? _lifetime.Token);
        Raise();
    }

    private void StartArtworkLoad(string id, int version, CancellationToken token)
    {
        _lastArtworkAttempt = DateTimeOffset.UtcNow;
        _artworkLoadTask = LoadArtworkAsync(id, version, token);
    }

    private async Task LoadArtworkAsync(string id, int version, CancellationToken token, int attempt = 0)
    {
        try
        {
            var data = await Task.Run(() => _media.ReadArtworkAsync(id, token), token);
            if (data is null)
            {
                if (attempt < 2 && !token.IsCancellationRequested && !_disposed && version == _artworkVersion)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(220 * (attempt + 1)), token);
                    await LoadArtworkAsync(id, version, token, attempt + 1);
                }
                return;
            }
            if (token.IsCancellationRequested || _disposed || version != _artworkVersion) return;
            using var stream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
            {
                writer.WriteBytes(data.Bytes);
                await writer.StoreAsync().AsTask(token);
            }
            stream.Seek(0);
            var bitmap = new BitmapImage { DecodePixelWidth = 160, DecodePixelHeight = 160 };
            await bitmap.SetSourceAsync(stream).AsTask(token);
            if (token.IsCancellationRequested || _disposed || version != _artworkVersion) return;
            _artwork = bitmap;
            PropertyChanged?.Invoke(this, new(nameof(Artwork)));
            var color = await _palette.ExtractAsync(data, token);
            if (color is null || token.IsCancellationRequested || _disposed || version != _artworkVersion) return;
            _ambientColor = Color.FromArgb(255, color.Value.Red, color.Value.Green, color.Value.Blue);
            PropertyChanged?.Invoke(this, new(nameof(AmbientColor)));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { ProbeLog.Write("Artwork", ex); }
    }

    public async Task SendAsync(MediaCommand command)
    {
        var selected = _selected;
        if (_disposed || _busy || selected is null) return;
        if (command == MediaCommand.Toggle && !selected.Capabilities.CanToggle)
            command = selected.Status == PlaybackStatus.Playing ? MediaCommand.Pause : MediaCommand.Play;
        _busy = true;
        _error = "";
        Raise();
        try
        {
            var result = await Task.Run(() => _media.ExecuteAsync(selected.Id, command, cancellationToken: _lifetime.Token), _lifetime.Token);
            if (!result.Succeeded) _error = TextCatalog.Get("CommandError");
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception ex) { ProbeLog.Write("Command", ex); _error = TextCatalog.Get("CommandError"); }
        finally { _busy = false; if (!_disposed) Raise(); }
    }


    public async Task SeekToPercentageAsync(double percentage)
    {
        var selected = _selected;
        if (selected is null || selected.Timeline.End <= selected.Timeline.Start) return;
        var position = PlaybackProgress.PositionAt(percentage, selected.Timeline.Start, selected.Timeline.End);
        await SeekToPositionAsync(position);
    }

    public async Task SeekToPositionAsync(TimeSpan position)
    {
        var selected = _selected;
        if (_disposed || _busy || selected is null || !selected.Capabilities.CanSeek ||
            selected.Timeline.End <= selected.Timeline.Start) return;
        position = position < selected.Timeline.Start ? selected.Timeline.Start :
            position > selected.Timeline.End ? selected.Timeline.End : position;
        _busy = true;
        _error = "";
        Raise();
        try
        {
            var result = await Task.Run(() => _media.ExecuteAsync(selected.Id, MediaCommand.Seek,
                position, _lifetime.Token), _lifetime.Token);
            if (!result.Succeeded) _error = TextCatalog.Get("CommandError");
            else
            {
                var timeline = selected.Timeline with { Position = position, LastUpdated = DateTimeOffset.UtcNow };
                _timeline.Reconcile(timeline, selected.Status == PlaybackStatus.Playing, selected.PlaybackRate);
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception ex) { ProbeLog.Write("Seek", ex); _error = TextCatalog.Get("CommandError"); }
        finally { _busy = false; if (!_disposed) Raise(); }
    }

    public async Task EnsurePlayingAsync()
    {
        var selected = _selected;
        if (_disposed || _busy || selected is null || selected.Status == PlaybackStatus.Playing) return;
        if (selected.Capabilities.CanPlay) await SendAsync(MediaCommand.Play);
        else if (selected.Capabilities.CanToggle) await SendAsync(MediaCommand.Toggle);
    }

    private void TimelineTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        if (_disposed || _selected is null || ProgressVisibility != Visibility.Visible) return;
        PropertyChanged?.Invoke(this, new(nameof(EstimatedPosition)));
        PropertyChanged?.Invoke(this, new(nameof(Progress)));
        PropertyChanged?.Invoke(this, new(nameof(PositionText)));
        PropertyChanged?.Invoke(this, new(nameof(DurationText)));
        PropertyChanged?.Invoke(this, new(nameof(ProgressLabel)));
    }

    private static string FormatTime(TimeSpan value) => value.TotalHours >= 1
        ? value.ToString(@"h\:mm\:ss")
        : value.ToString(@"m\:ss");

    private void Raise() => PropertyChanged?.Invoke(this, new(null));

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _media.SnapshotChanged -= OnSnapshotChanged;
        _media.Diagnostic -= OnDiagnostic;
        _timelineTimer.Stop();
        _timelineTimer.Tick -= TimelineTimer_Tick;
        await _lifetime.CancelAsync();
        _artworkCancellation?.Cancel();
        if (_startTask is not null) await _startTask;
        if (_artworkLoadTask is not null)
        {
            try { await _artworkLoadTask; }
            catch (OperationCanceledException) { }
        }
        await _media.DisposeAsync();
        _artworkCancellation?.Dispose();
        _lifetime.Dispose();
    }
}
