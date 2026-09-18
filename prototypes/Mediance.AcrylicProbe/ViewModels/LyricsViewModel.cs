using System.ComponentModel;
using Mediance.Core.Lyrics;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace Mediance.AcrylicProbe.ViewModels;

public sealed record LyricsLinesChangedEventArgs(
    string PreviousLine,
    string CurrentLine,
    string NextLine,
    bool Forward);

public sealed class LyricsViewModel : INotifyPropertyChanged, IDisposable
{
    private static readonly TimeSpan AutomaticSyncRetryDelay = TimeSpan.FromSeconds(12);
    private readonly ILyricsService _lyrics;
    private readonly IEditableLyricsService? _editableLyrics;
    private readonly IAutomaticLyricsSynchronizer? _automaticLyrics;
    private readonly PlayerViewModel _player;
    private readonly DispatcherQueue _dispatcher;
    private readonly DispatcherQueueTimer _timer;
    private readonly DispatcherQueueTimer _followTimer;
    private CancellationTokenSource? _loadCancellation;
    private CancellationTokenSource? _automaticCancellation;
    private LyricsDocument _document = LyricsDocument.Unavailable;
    private string? _trackIdentity;
    private string _status = "";
    private string _previous = "";
    private string _current = "";
    private string _next = "";
    private int _activeLineIndex = -1;
    private int _displayLineIndex = int.MinValue;
    private bool _manualScrolling;
    private string[] _authorLines = [];
    private readonly List<TimeSpan> _authoredTimings = [];
    private int _authorIndex;
    private bool _authoring;
    private bool _authorBusy;
    private string? _authorTrackIdentity;
    private LyricsDocument? _retimingOriginal;
    private bool _visible;
    private bool _disposed;
    private bool _automaticSyncEnabled = true;
    private bool _automaticBusy;
    private string? _automaticAttemptIdentity;
    private AutomaticLyricsSyncStage? _automaticStage;
    private double? _automaticFraction;
    private DateTimeOffset _automaticCaptureStartedUtc;
    private TimeSpan _automaticCaptureOffset;
    private TimeSpan _lead = TimeSpan.FromMilliseconds(500);

    public LyricsViewModel(ILyricsService lyrics, PlayerViewModel player, DispatcherQueue dispatcher,
        IAutomaticLyricsSynchronizer? automaticLyrics = null)
    {
        _lyrics = lyrics;
        _editableLyrics = lyrics as IEditableLyricsService;
        _automaticLyrics = automaticLyrics;
        _player = player;
        _dispatcher = dispatcher;
        _trackIdentity = player.TrackIdentity;
        _player.PropertyChanged += Player_PropertyChanged;
        _timer = dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(200);
        _timer.IsRepeating = true;
        _timer.Tick += Timer_Tick;
        _followTimer = dispatcher.CreateTimer();
        _followTimer.Interval = TimeSpan.FromSeconds(3.5);
        _followTimer.IsRepeating = false;
        _followTimer.Tick += FollowTimer_Tick;
    }

    public Visibility PanelVisibility => _visible ? Visibility.Visible : Visibility.Collapsed;
    public Visibility SyncedVisibility => _document.Kind == LyricsKind.Synced ? Visibility.Visible : Visibility.Collapsed;
    public Visibility AuthorVisibility => _document.Kind == LyricsKind.Plain && _authorLines.Length > 0
        ? Visibility.Visible : Visibility.Collapsed;
    public Visibility AuthoringControlsVisibility => _authoring ? Visibility.Visible : Visibility.Collapsed;
    public Visibility RetimingVisibility => _document.Kind == LyricsKind.Synced && _document.IsUserTimed
        ? Visibility.Visible : Visibility.Collapsed;
    public Visibility MessageVisibility => _document.Kind is LyricsKind.Plain or LyricsKind.Instrumental or LyricsKind.Unavailable ||
        !string.IsNullOrEmpty(_status) ? Visibility.Visible : Visibility.Collapsed;
    public string PreviousLine => _previous;
    public string CurrentLine => _current;
    public string NextLine => _next;
    public string Status => _status;
    public string AuthorPreviousLine => _authorIndex > 0 && _authorIndex - 1 < _authorLines.Length
        ? _authorLines[_authorIndex - 1] : "";
    public string AuthorCurrentLine => _authorIndex >= 0 && _authorIndex < _authorLines.Length
        ? _authorLines[_authorIndex] : "";
    public string AuthorNextLine => _authorIndex + 1 < _authorLines.Length
        ? _authorLines[_authorIndex + 1] : "";
    public string AuthorProgress => _authoring
        ? string.Format(Localization.TextCatalog.Get("LyricsManualProgress"), Math.Min(_authorIndex + 1, _authorLines.Length), _authorLines.Length)
        : string.Format(Localization.TextCatalog.Get("LyricsManualLineCount"), _authorLines.Length);
    public string AuthorButtonLabel => Localization.TextCatalog.Get(_authoring
        ? "LyricsManualMark" : "LyricsManualStart");
    public bool AuthorButtonEnabled => !_authorBusy && _document.Kind == LyricsKind.Plain && _authorLines.Length > 0;
    public string ToggleLabel => Localization.TextCatalog.Get(_visible ? "HideLyrics" : "Lyrics");
    public bool CanScrollSynced => _visible && _document.Kind == LyricsKind.Synced && _document.Lines.Count > 0;
    public bool IsAuthoring => _authoring;
    private bool IsFlowing => _document.Kind == LyricsKind.Synced;
    public bool IsVisible => _visible;
    public bool AutomaticSyncActive => _automaticBusy;
    public string AutomaticSyncStageText => _automaticBusy ? _status : Localization.TextCatalog.Get("LyricsSyncIdle");
    public double AutomaticSyncProgress => Math.Clamp((_automaticFraction ?? 0) * 100, 0, 100);
    public bool AutomaticSyncIndeterminate => _automaticBusy && _automaticFraction is null;
    public Visibility AutomaticSyncProgressVisibility => _automaticBusy ? Visibility.Visible : Visibility.Collapsed;
    public bool AutomaticSyncEnabled
    {
        get => _automaticSyncEnabled;
        set
        {
            if (_automaticSyncEnabled == value) return;
            _automaticSyncEnabled = value;
            if (!value)
            {
                _automaticCancellation?.Cancel();
                if (_document.Kind == LyricsKind.Plain)
                    _status = Localization.TextCatalog.Get("LyricsManualAvailable");
            }
            else TryStartAutomaticSync();
            Raise();
        }
    }
    public TimeSpan Lead
    {
        get => _lead;
        set
        {
            if (value == _lead) return;
            _lead = value;
            UpdateLines();
        }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<LyricsLinesChangedEventArgs>? LinesChanged;

    public Task ToggleAsync() => SetVisibleAsync(!_visible);

    public async Task SetVisibleAsync(bool visible)
    {
        if (_disposed || _visible == visible) return;
        _visible = visible;
        if (_visible)
        {
            _timer.Start();
            await ReloadAsync();
        }
        else
        {
            _timer.Stop();
            _followTimer.Stop();
            _manualScrolling = false;
            CancelAuthoring(false);
            _loadCancellation?.Cancel();
            _automaticCancellation?.Cancel();
        }
        Raise();
    }

    private async Task ReloadAsync()
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = new();
        var token = _loadCancellation.Token;
        var query = _player.LyricsQuery;
        _trackIdentity = _player.TrackIdentity;
        _document = LyricsDocument.Unavailable;
        _previous = _current = _next = "";
        _activeLineIndex = -1;
        _displayLineIndex = int.MinValue;
        _manualScrolling = false;
        CancelAuthoring(false);
        _followTimer.Stop();
        if (query is null)
        {
            _status = Localization.TextCatalog.Get("LyricsNeedTrack");
            Raise();
            return;
        }

        _status = Localization.TextCatalog.Get("LyricsLoading");
        Raise();
        try
        {
            var document = await _lyrics.FindAsync(query, token);
            if (token.IsCancellationRequested || _disposed || _trackIdentity != _player.TrackIdentity) return;
            ApplyDocument(document);
            if (document.Kind == LyricsKind.Plain || document.IsUserTimed)
                _ = RetrySourceAuthoredLyricsAsync(query, _trackIdentity, token);
            if (document.Kind == LyricsKind.Plain) TryStartAutomaticSync();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            // HttpRequestException messages can contain the request URL and
            // therefore listening metadata. Keep diagnostics content-free.
            ProbeLog.Write($"Lyrics: {ex.GetType().Name} (0x{ex.HResult:X8})");
            _document = LyricsDocument.Unavailable;
            _status = Localization.TextCatalog.Get("LyricsError");
            Raise();
        }
    }

    private async Task RetrySourceAuthoredLyricsAsync(LyricsQuery query, string? trackIdentity,
        CancellationToken token)
    {
        try
        {
            await Task.Delay(AutomaticSyncRetryDelay, token);
            if (token.IsCancellationRequested || _disposed || !_visible || _authoring || _authorBusy ||
                trackIdentity != _player.TrackIdentity) return;

            var refreshed = await _lyrics.FindAsync(query, token);
            if (token.IsCancellationRequested || _disposed || !_visible || _authoring || _authorBusy ||
                trackIdentity != _player.TrackIdentity || refreshed.Kind != LyricsKind.Synced ||
                refreshed.IsUserTimed) return;

            ApplyDocument(refreshed);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            ProbeLog.Write($"LyricsRetry: {ex.GetType().Name} (0x{ex.HResult:X8})");
        }
    }

    private void ApplyDocument(LyricsDocument document)
    {
        if (document.Kind != LyricsKind.Plain) _automaticCancellation?.Cancel();
        _document = document;
        _authorLines = document.Kind == LyricsKind.Plain
            ? PlainLyricsTimeline.Clean(document.PlainText).Split('\n',
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            : [];
        _authorIndex = 0;
        _status = document.Kind switch
        {
            LyricsKind.Instrumental => Localization.TextCatalog.Get("LyricsInstrumental"),
            LyricsKind.Unavailable => Localization.TextCatalog.Get("LyricsUnavailable"),
            LyricsKind.Plain => Localization.TextCatalog.Get(_automaticSyncEnabled
                ? "LyricsAutoWaiting" : "LyricsManualAvailable"),
            _ => ""
        };
        _activeLineIndex = -1;
        _displayLineIndex = int.MinValue;
        _previous = _current = _next = "";
        _manualScrolling = false;
        UpdateLines();
        Raise();
    }

    private void Player_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PlayerViewModel.EstimatedPosition) or nameof(PlayerViewModel.IsPlaying) or null)
            TryStartAutomaticSync();
        if (e.PropertyName is not null and not nameof(PlayerViewModel.TrackIdentity)) return;
        if (_trackIdentity == _player.TrackIdentity) return;
        if (_automaticStage == AutomaticLyricsSyncStage.Capturing) _automaticCancellation?.Cancel();
        _automaticAttemptIdentity = null;
        CancelAuthoring(false);
        _trackIdentity = _player.TrackIdentity;
        if (_visible) _ = ReloadAsync();
    }
    private void Timer_Tick(DispatcherQueueTimer sender, object args)
    {
        ValidateAutomaticCapture();
        UpdateLines();
    }
    private void FollowTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        _manualScrolling = false;
        if (IsFlowing && _document.Lines.Count > 0)
            SetDisplayedLine(_activeLineIndex);
    }

    public bool Scroll(int wheelDelta)
    {
        if (!CanScrollSynced || wheelDelta == 0) return false;
        var origin = _displayLineIndex >= 0 ? _displayLineIndex : Math.Max(0, _activeLineIndex);
        var target = LyricsTimeline.BrowseLineIndex(_displayLineIndex, _activeLineIndex,
            _document.Lines.Count, wheelDelta);
        _manualScrolling = true;
        _followTimer.Stop();
        _followTimer.Start();
        if (target != origin) SetDisplayedLine(target);
        return true;
    }

    public async Task SeekVisibleLineAsync(int relativeIndex)
    {
        if (!CanScrollSynced || _displayLineIndex < -1) return;
        var index = _displayLineIndex + relativeIndex;
        if (index < 0 || index >= _document.Lines.Count) return;
        _followTimer.Stop();
        _manualScrolling = false;
        _activeLineIndex = index;
        SetDisplayedLine(index);
        await _player.SeekToPositionAsync(_document.Lines[index].Start);
    }

    public async Task StartOrMarkAuthoringAsync()
    {
        if (_authorBusy || _editableLyrics is null || _document.Kind != LyricsKind.Plain || _authorLines.Length == 0)
            return;
        _authorBusy = true;
        _automaticCancellation?.Cancel();
        try
        {
            if (!_authoring)
            {
                if (!_player.CanSeek)
                {
                    _status = Localization.TextCatalog.Get("LyricsManualSeekRequired");
                    return;
                }
                _authoring = true;
                _authorIndex = 0;
                _authoredTimings.Clear();
                _authorTrackIdentity = _player.TrackIdentity;
                _status = Localization.TextCatalog.Get("LyricsManualReady");
                Raise();
                await _player.SeekToPositionAsync(TimeSpan.Zero);
                await _player.EnsurePlayingAsync();
                return;
            }

            if (_authorTrackIdentity != _player.TrackIdentity)
            {
                CancelAuthoring();
                return;
            }
            var position = _player.EstimatedPosition;
            if (_authoredTimings.Count > 0 && position <= _authoredTimings[^1])
                position = _authoredTimings[^1] + TimeSpan.FromMilliseconds(50);
            _authoredTimings.Add(position < TimeSpan.Zero ? TimeSpan.Zero : position);
            _authorIndex++;

            if (_authorIndex < _authorLines.Length)
            {
                _status = Localization.TextCatalog.Get("LyricsManualRecording");
                return;
            }

            var query = _player.LyricsQuery;
            if (query is null || _authorTrackIdentity != _player.TrackIdentity)
            {
                CancelAuthoring();
                return;
            }
            var saved = await _editableLyrics.SaveTimingAsync(query, _document.PlainText ?? "", _authoredTimings);
            if (_authorTrackIdentity != _player.TrackIdentity) return;
            _document = saved;
            _authoring = false;
            _retimingOriginal = null;
            _authorLines = [];
            _authoredTimings.Clear();
            _authorIndex = 0;
            _authorTrackIdentity = null;
            _status = "";
            _activeLineIndex = -1;
            _displayLineIndex = int.MinValue;
            UpdateLines();
        }
        catch (Exception ex)
        {
            ProbeLog.Write($"LyricsTiming: {ex.GetType().Name} (0x{ex.HResult:X8})");
            CancelAuthoring(false);
            _status = Localization.TextCatalog.Get("LyricsManualSaveError");
        }
        finally
        {
            _authorBusy = false;
            Raise();
        }
    }

    public async Task RestartAuthoringAsync()
    {
        if (!_authoring) return;
        _authoring = false;
        _authorBusy = false;
        await StartOrMarkAuthoringAsync();
    }

    public async Task BeginRetimingAsync()
    {
        if (_authorBusy || !_document.IsUserTimed || string.IsNullOrWhiteSpace(_document.PlainText)) return;
        _retimingOriginal = _document;
        _document = new(LyricsKind.Plain, [], _retimingOriginal.PlainText);
        _authorLines = PlainLyricsTimeline.Clean(_document.PlainText).Split('\n',
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        _authorIndex = 0;
        _status = Localization.TextCatalog.Get("LyricsManualAvailable");
        Raise();
        await StartOrMarkAuthoringAsync();
    }

    public void CancelAuthoring(bool notify = true)
    {
        _authoring = false;
        _authorBusy = false;
        _authorIndex = 0;
        _authoredTimings.Clear();
        _authorTrackIdentity = null;
        if (_retimingOriginal is not null)
        {
            _document = _retimingOriginal;
            _retimingOriginal = null;
            _authorLines = [];
            _status = "";
        }
        else if (_document.Kind == LyricsKind.Plain)
            _status = Localization.TextCatalog.Get(_automaticSyncEnabled
                ? "LyricsAutoWaiting" : "LyricsManualAvailable");
        if (notify) Raise();
    }

    private void UpdateLines()
    {
        if (!IsFlowing || _document.Lines.Count == 0) return;
        var displayPosition = _player.EstimatedPosition + _lead;
        if (displayPosition < TimeSpan.Zero) displayPosition = TimeSpan.Zero;
        var index = LyricsTimeline.ActiveLineIndex(_document.Lines, displayPosition);
        if (index == _activeLineIndex) return;
        _activeLineIndex = index;
        if (!_manualScrolling) SetDisplayedLine(index);
    }

    private void SetDisplayedLine(int index)
    {
        var previous = index > 0 ? _document.Lines[index - 1].Text : "";
        var current = index >= 0 ? _document.Lines[index].Text : "";
        var nextIndex = index + 1;
        var next = nextIndex >= 0 && nextIndex < _document.Lines.Count ? _document.Lines[nextIndex].Text : "";
        if (index == _displayLineIndex && previous == _previous && current == _current && next == _next) return;
        var oldPrevious = _previous;
        var oldCurrent = _current;
        var oldNext = _next;
        var forward = _displayLineIndex < 0 || index >= _displayLineIndex;
        _displayLineIndex = index;
        _previous = previous;
        _current = current;
        _next = next;
        Raise();
        LinesChanged?.Invoke(this, new(oldPrevious, oldCurrent, oldNext, forward));
    }
    private void Raise() => PropertyChanged?.Invoke(this, new(null));

    private void TryStartAutomaticSync()
    {
        if (_disposed || !_visible || !_automaticSyncEnabled || _automaticLyrics is null ||
            _editableLyrics is null || _automaticBusy || _authoring || _authorBusy ||
            _document.Kind != LyricsKind.Plain || string.IsNullOrWhiteSpace(_document.PlainText)) return;
        var identity = _player.TrackIdentity;
        var query = _player.LyricsQuery;
        var source = _player.SourceAppId;
        if (identity is null || query is null || string.IsNullOrWhiteSpace(source) ||
            _automaticAttemptIdentity == identity) return;
        var position = _player.EstimatedPosition;
        if (!_player.IsPlaying || position < TimeSpan.Zero || position > TimeSpan.FromSeconds(8))
        {
            _status = Localization.TextCatalog.Get("LyricsAutoWaiting");
            return;
        }

        _automaticAttemptIdentity = identity;
        _automaticBusy = true;
        _automaticStage = AutomaticLyricsSyncStage.Capturing;
        _automaticFraction = null;
        _automaticCaptureStartedUtc = DateTimeOffset.UtcNow;
        _automaticCaptureOffset = position;
        _automaticCancellation?.Cancel();
        _automaticCancellation?.Dispose();
        _automaticCancellation = new();
        _ = RunAutomaticSyncAsync(new(source, query, _document.PlainText, position), identity,
            _automaticCancellation.Token);
    }

    private async Task RunAutomaticSyncAsync(
        AutomaticLyricsSyncRequest request, string identity, CancellationToken token)
    {
        void ProgressChanged(object? sender, AutomaticLyricsSyncProgress progress)
        {
            _automaticStage = progress.Stage;
            _automaticFraction = progress.Fraction;
            _dispatcher.TryEnqueue(() =>
            {
                if (_disposed || identity != _player.TrackIdentity || _document.Kind != LyricsKind.Plain) return;
                _status = Localization.TextCatalog.Get(progress.Stage switch
                {
                    AutomaticLyricsSyncStage.Capturing => "LyricsAutoCapturing",
                    AutomaticLyricsSyncStage.DownloadingModel => "LyricsAutoDownloading",
                    AutomaticLyricsSyncStage.Transcribing => "LyricsAutoTranscribing",
                    _ => "LyricsAutoAligning"
                });
                Raise();
            });
        }

        _automaticLyrics!.ProgressChanged += ProgressChanged;
        try
        {
            var alignment = await _automaticLyrics.SynchronizeAsync(request, token);
            if (token.IsCancellationRequested || alignment is null) return;
            var lineCount = PlainLyricsTimeline.Clean(request.PlainText).Split('\n',
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Length;
            if (!alignment.IsReliable(lineCount))
            {
                _dispatcher.TryEnqueue(() =>
                {
                    if (!_disposed && identity == _player.TrackIdentity && _document.Kind == LyricsKind.Plain)
                    {
                        _status = Localization.TextCatalog.Get("LyricsAutoLowConfidence");
                        Raise();
                    }
                });
                return;
            }

            var saved = await _editableLyrics!.SaveTimingAsync(request.Query, request.PlainText,
                alignment.LineStarts, token);
            _dispatcher.TryEnqueue(() =>
            {
                if (!_disposed && identity == _player.TrackIdentity && _document.Kind == LyricsKind.Plain)
                    ApplyDocument(saved);
            });
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            ProbeLog.Write($"LyricsAutoSync: {ex.GetType().Name} (0x{ex.HResult:X8})");
            _dispatcher.TryEnqueue(() =>
            {
                if (!_disposed && identity == _player.TrackIdentity && _document.Kind == LyricsKind.Plain)
                {
                    _status = Localization.TextCatalog.Get("LyricsAutoError");
                    Raise();
                }
            });
        }
        finally
        {
            _automaticLyrics.ProgressChanged -= ProgressChanged;
            _automaticStage = null;
            _automaticFraction = null;
            _automaticBusy = false;
            if (token.IsCancellationRequested && identity == _player.TrackIdentity)
                _automaticAttemptIdentity = null;
            _dispatcher.TryEnqueue(() => { Raise(); TryStartAutomaticSync(); });
        }
    }

    private void ValidateAutomaticCapture()
    {
        if (!_automaticBusy || _automaticStage != AutomaticLyricsSyncStage.Capturing) return;
        if (_automaticAttemptIdentity != _player.TrackIdentity || !_player.IsPlaying)
        {
            _automaticCancellation?.Cancel();
            return;
        }
        var expected = _automaticCaptureOffset + (DateTimeOffset.UtcNow - _automaticCaptureStartedUtc);
        if (Math.Abs((_player.EstimatedPosition - expected).TotalSeconds) > 2.5)
            _automaticCancellation?.Cancel();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _timer.Tick -= Timer_Tick;
        _followTimer.Stop();
        _followTimer.Tick -= FollowTimer_Tick;
        _player.PropertyChanged -= Player_PropertyChanged;
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _automaticCancellation?.Cancel();
        _automaticCancellation?.Dispose();
    }
}
