using System.ComponentModel;
using Mediance.Core.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Mediance.AcrylicProbe.ViewModels;

public sealed record ThemeOption(ThemePreset Value, string Label);
public sealed record LyricsLineOption(int Value, string Label);
public sealed record ViewModeOption(WidgetViewMode Value, string Label);

public sealed class SettingsViewModel(JsonSettingsStore? store) : INotifyPropertyChanged, IAsyncDisposable
{
    private WidgetSettings _data = new();
    private CancellationTokenSource? _saveCancellation;
    private Task _saveTask = Task.CompletedTask;
    private bool _disposed;
    private bool _loaded;
    private int _revision;
    private string _error = "";
    private string _themeTransferStatus = "";
    private IReadOnlyList<ToggleSetting>? _windowOptions;
    private IReadOnlyList<ToggleSetting>? _contentOptions;
    private IReadOnlyList<ToggleSetting>? _controlOptions;
    private IReadOnlyList<ToggleSetting>? _utilityOptions;
    public IReadOnlyList<ToggleSetting> WindowOptions => _windowOptions ??=
    [
        new(Localization.TextCatalog.Get("LockPosition"), () => IsLocked, v => IsLocked = v),
        new(Localization.TextCatalog.Topmost, () => AlwaysOnTop, v => AlwaysOnTop = v),
        new(Localization.TextCatalog.Get("AmbientGlow"), () => EnableAmbientGlow, v => EnableAmbientGlow = v),
        new(Localization.TextCatalog.Get("WheelVolume"), () => EnableWheelVolume, v => EnableWheelVolume = v),
        new(Localization.TextCatalog.Get("CloseToTray"), () => CloseToTray, v => CloseToTray = v),
        new(Localization.TextCatalog.Solid, () => SolidBackground, v => SolidBackground = v),
        new(Localization.TextCatalog.Get("ShowBorder"), () => ShowBorder, v => ShowBorder = v)
    ];
    public IReadOnlyList<ToggleSetting> ContentOptions => _contentOptions ??=
    [
        new(Localization.TextCatalog.Get("ShowArtwork"), () => ShowArtwork, v => ShowArtwork = v),
        new(Localization.TextCatalog.Get("ShowProgress"), () => ShowProgress, v => ShowProgress = v),
        new(Localization.TextCatalog.Get("ShowTitle"), () => ShowTitle, v => ShowTitle = v),
        new(Localization.TextCatalog.Get("ShowArtist"), () => ShowArtist, v => ShowArtist = v),
        new(Localization.TextCatalog.Get("ShowSource"), () => ShowSource, v => ShowSource = v)
    ];
    public IReadOnlyList<ToggleSetting> ControlOptions => _controlOptions ??=
    [
        new(Localization.TextCatalog.Get("ShowControls"), () => ShowControls, v => ShowControls = v)
    ];
    public IReadOnlyList<ToggleSetting> UtilityOptions => _utilityOptions ??=
    [
        new(Localization.TextCatalog.Get("ShowAudioOutput"), () => ShowAudioOutput, v => ShowAudioOutput = v),
        new(Localization.TextCatalog.Get("ShowLyricsButton"), () => ShowLyricsButton, v => ShowLyricsButton = v)
    ];
    public WidgetSettings Data => _data;
    public string Error => _error;
    public string ThemeTransferStatus => _themeTransferStatus;
    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? Changed;
    public bool AlwaysOnTop { get => _data.AlwaysOnTop; set => Change(_data with { AlwaysOnTop = value }); }
    public bool IsLocked { get => _data.IsLocked; set => Change(_data with { IsLocked = value }); }
    public bool SolidBackground { get => _data.SolidBackground; set => Change(_data with { SolidBackground = value }); }
    public double GlassIntensity { get => _data.GlassIntensity; set => Change(_data with { GlassIntensity = value }); }
    public bool ShowArtwork { get => _data.ShowArtwork; set => Change(_data with { ShowArtwork = value }); }
    public bool ShowProgress { get => _data.ShowProgress; set => Change(_data with { ShowProgress = value }); }
    public bool EnableAmbientGlow { get => _data.EnableAmbientGlow; set => Change(_data with { EnableAmbientGlow = value }); }
    public bool EnableWheelVolume { get => _data.EnableWheelVolume; set => Change(_data with { EnableWheelVolume = value }); }
    public bool CloseToTray { get => _data.CloseToTray; set => Change(_data with { CloseToTray = value }); }
    public bool ShowTitle { get => _data.ShowTitle; set => Change(_data with { ShowTitle = value }); }
    public bool ShowArtist { get => _data.ShowArtist; set => Change(_data with { ShowArtist = value }); }
    public bool ShowSource { get => _data.ShowSource; set => Change(_data with { ShowSource = value }); }
    public bool ShowControls { get => _data.ShowControls; set => Change(_data with { ShowControls = value }); }
    public bool ShowBrand { get => _data.ShowBrand; set => Change(_data with { ShowBrand = value }); }
    public bool ShowBorder { get => _data.ShowBorder; set => Change(_data with { ShowBorder = value }); }
    public bool ShowPlaybackStatus { get => _data.ShowPlaybackStatus; set => Change(_data with { ShowPlaybackStatus = value }); }
    public bool ShowLyricsButton { get => _data.ShowLyricsButton; set => Change(_data with { ShowLyricsButton = value }); }
    public bool LyricsOpen { get => _data.LyricsOpen; set => Change(_data with { LyricsOpen = value }); }
    public int LyricsLineCount { get => _data.LyricsLineCount; set => Change(_data with { LyricsLineCount = value }); }
    public bool EnableAutomaticLyricsSync { get => _data.EnableAutomaticLyricsSync; set => Change(_data with { EnableAutomaticLyricsSync = value }); }
    public bool StartWithWindows { get => _data.StartWithWindows; set => Change(_data with { StartWithWindows = value }); }
    public ThemePreset Theme { get => _data.Theme; set => Change(_data with { Theme = value }); }
    public WidgetViewMode ViewMode { get => _data.ViewMode; set => Change(_data with { ViewMode = value }); }
    public double AlbumBlur { get => _data.AlbumBlur; set => Change(_data with { AlbumBlur = value }); }
    public double AlbumZoom { get => _data.AlbumZoom; set => Change(_data with { AlbumZoom = value }); }
    public double AlbumDarkness { get => _data.AlbumDarkness; set => Change(_data with { AlbumDarkness = value }); }
    public bool ShowAudioOutput { get => _data.ShowAudioOutput; set => Change(_data with { ShowAudioOutput = value }); }
    public double WindowWidth { get => _data.WindowWidth; set => Change(_data with { WindowWidth = value }); }
    public double ArtworkSize { get => _data.ArtworkSize; set => Change(_data with { ArtworkSize = value }); }
    public double TextScale { get => _data.TextScale; set => Change(_data with { TextScale = value }); }
    public double ControlSize { get => _data.ControlSize; set => Change(_data with { ControlSize = value }); }
    public double LyricsLeadMilliseconds { get => _data.LyricsLeadMilliseconds; set => Change(_data with { LyricsLeadMilliseconds = value }); }
    public int? WindowX => _data.WindowX;
    public int? WindowY => _data.WindowY;
    public string? LastMonitorId => _data.LastMonitorId;
    public IReadOnlyDictionary<string, SavedWindowPlacement> MonitorPlacements => _data.MonitorPlacements;
    public IReadOnlyList<ThemeOption> ThemeOptions { get; } =
    [
        new(ThemePreset.Midnight, Localization.TextCatalog.Get("ThemeMidnight")),
        new(ThemePreset.Prism, Localization.TextCatalog.Get("ThemePrism")),
        new(ThemePreset.ClearGlass, Localization.TextCatalog.Get("ThemeClearGlass")),
        new(ThemePreset.Album, Localization.TextCatalog.Get("ThemeAlbum"))
    ];
    public ThemeOption SelectedTheme
    {
        get => ThemeOptions.First(x => x.Value == Theme);
        set { if (value is not null) Theme = value.Value; }
    }
    public IReadOnlyList<ViewModeOption> ViewModeOptions { get; } =
    [
        new(WidgetViewMode.Standard, Localization.TextCatalog.Get("ViewModeStandard")),
        new(WidgetViewMode.Micro, Localization.TextCatalog.Get("ViewModeMicro")),
        new(WidgetViewMode.CoverControls, Localization.TextCatalog.Get("ViewModeCoverControls")),
        new(WidgetViewMode.VerticalLyrics, Localization.TextCatalog.Get("ViewModeVerticalLyrics"))
    ];
    public ViewModeOption SelectedViewMode
    {
        get => ViewModeOptions.First(x => x.Value == ViewMode);
        set { if (value is not null) ViewMode = value.Value; }
    }
    public IReadOnlyList<LyricsLineOption> LyricsLineOptions { get; } =
    [
        new(2, Localization.TextCatalog.Get("LyricsTwoLines")),
        new(3, Localization.TextCatalog.Get("LyricsThreeLines"))
    ];
    public LyricsLineOption SelectedLyricsLineOption
    {
        get => LyricsLineOptions.First(x => x.Value == LyricsLineCount);
        set { if (value is not null) LyricsLineCount = value.Value; }
    }
    public bool GlassEnabled => !SolidBackground;
    public bool AlbumControlsEnabled => Theme == ThemePreset.Album;
    public bool IsMicroMode => ViewMode == WidgetViewMode.Micro;
    public bool IsCoverControlsMode => ViewMode == WidgetViewMode.CoverControls;
    public bool IsVerticalLyricsMode => ViewMode == WidgetViewMode.VerticalLyrics;
    public string GlassValue => $"{GlassIntensity:0}%";
    public string AlbumBlurValue => $"{AlbumBlur:0} px";
    public string AlbumZoomValue => $"{AlbumZoom:0}%";
    public string AlbumDarknessValue => $"{AlbumDarkness:0}%";
    public double AlbumZoomScale => AlbumZoom / 100;
    public Brush AlbumDarknessBrush => new SolidColorBrush(Color.FromArgb(
        (byte)Math.Round(AlbumDarkness / 100 * 255), 5, 7, 11));
    public string WidthValue => $"{WindowWidth:0} px";
    public string LockGlyph => IsLocked ? "\uE72E" : "\uE785";
    public string LockLabel => Localization.TextCatalog.Get(IsLocked ? "UnlockPosition" : "LockPosition");
    public string ArtworkValue => $"{ArtworkSize:0} px";
    public string TextValue => $"{TextScale:0}%";
    public string ControlValue => $"{ControlSize:0} px";
    public string LyricsTimingValue => LyricsLeadMilliseconds switch
    {
        > 0 => string.Format(Localization.TextCatalog.Get("LyricsTimingEarly"), LyricsLeadMilliseconds / 1000),
        < 0 => string.Format(Localization.TextCatalog.Get("LyricsTimingLate"), -LyricsLeadMilliseconds / 1000),
        _ => Localization.TextCatalog.Get("LyricsTimingExact")
    };
    public double TitleFontSize => 22 * TextScale / 100;
    public double ArtistFontSize => 13 * TextScale / 100;
    public double SourceFontSize => 11 * TextScale / 100;
    public double TransportIconSize => ControlSize * 0.39;
    public CornerRadius PlayCornerRadius => new(ControlSize / 2);
    public CornerRadius SurfaceCornerRadius => new(IsMicroMode ? 18 : ViewMode == WidgetViewMode.Standard ? 8 : 14);
    public bool HasMetadata => ShowTitle || ShowArtist || ShowSource;
    public bool HasMainText => HasMetadata || ShowBrand;
    public double ArtworkGap => ShowArtwork && HasMainText ? 18 : 0;
    public Visibility ArtworkVisibility => Visible(ShowArtwork);
    public Visibility ProgressVisibility => Visible(ShowProgress);
    public Visibility AmbientGlowVisibility => Visible(!IsMicroMode && EnableAmbientGlow);
    public Visibility AlbumThemeVisibility => Visible(!IsMicroMode && Theme == ThemePreset.Album);
    public Visibility StandardModeVisibility => Visible(ViewMode == WidgetViewMode.Standard);
    public Visibility MicroModeVisibility => Visible(IsMicroMode);
    public Visibility CoverControlsModeVisibility => Visible(IsCoverControlsMode);
    public Visibility VerticalLyricsModeVisibility => Visible(IsVerticalLyricsMode);
    public Visibility TitleVisibility => Visible(ShowTitle);
    public Visibility ArtistVisibility => Visible(ShowArtist);
    public Visibility SourceVisibility => Visible(ShowSource);
    public Visibility MetadataVisibility => Visible(HasMainText);
    public Visibility TrackVisibility => Visible(ShowArtwork || HasMainText);
    public Visibility MainRowVisibility => Visible(ShowArtwork || HasMainText || ShowControls);
    public Visibility ControlsVisibility => Visible(ShowControls);
    public Visibility BrandVisibility => Visible(ShowBrand);
    public Visibility StatusVisibility => Visible(ShowPlaybackStatus);
    public Visibility LyricsButtonVisibility => Visible(ShowLyricsButton);
    public Visibility AudioOutputVisibility => Visible(ShowAudioOutput);
    public Visibility PreviousLyricVisibility => Visible(LyricsLineCount == 3);
    private static Visibility Visible(bool show) => show ? Visibility.Visible : Visibility.Collapsed;

    public async Task LoadAsync()
    {
        if (_loaded) return;
        _loaded = true;
        var revision = _revision;
        try
        {
            if (store is not null)
            {
                var loaded = await store.LoadAsync();
                if (_revision == revision) { _data = loaded; Notify(); }
            }
        }
        catch (Exception ex) { Report(ex); }
    }

    private void Change(WidgetSettings data)
    {
        data = data.Normalize();
        if (_disposed || data == _data) return;
        _data = data;
        _revision++;
        Notify();
        if (store is null) return;
        _saveCancellation?.Cancel();
        _saveCancellation?.Dispose();
        _saveCancellation = new();
        _saveTask = SaveLaterAsync(data, _saveCancellation.Token);
    }

    private async Task SaveLaterAsync(WidgetSettings data, CancellationToken token)
    {
        try
        {
            await Task.Delay(300, token);
            await store!.SaveAsync(data, token);
            _error = "";
            PropertyChanged?.Invoke(this, new(nameof(Error)));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { Report(ex); }
    }

    private void Report(Exception ex)
    {
        ProbeLog.Write("Settings", ex);
        _error = Localization.TextCatalog.Get("SettingsSaveError");
        PropertyChanged?.Invoke(this, new(nameof(Error)));
    }
    private void Notify()
    {
        PropertyChanged?.Invoke(this, new(null));
        if (_windowOptions is not null) foreach (var option in _windowOptions) option.Refresh();
        if (_contentOptions is not null) foreach (var option in _contentOptions) option.Refresh();
        if (_controlOptions is not null) foreach (var option in _controlOptions) option.Refresh();
        if (_utilityOptions is not null) foreach (var option in _utilityOptions) option.Refresh();
        Changed?.Invoke(this, EventArgs.Empty);
    }
    public void Reset() => Change(new());
    public string ExportThemeJson()
    {
        _themeTransferStatus = Localization.TextCatalog.Get("ThemeExported");
        PropertyChanged?.Invoke(this, new(nameof(ThemeTransferStatus)));
        return ThemeProfile.FromSettings(_data).ToJson();
    }
    public void ImportThemeJson(string json)
    {
        Change(ThemeProfile.Parse(json).ApplyTo(_data));
        _themeTransferStatus = Localization.TextCatalog.Get("ThemeImported");
        PropertyChanged?.Invoke(this, new(nameof(ThemeTransferStatus)));
    }
    public void ReportThemeTransferError()
    {
        _themeTransferStatus = Localization.TextCatalog.Get("ThemeTransferError");
        PropertyChanged?.Invoke(this, new(nameof(ThemeTransferStatus)));
    }
    public void SetShortcut(HotkeyGesture gesture)
    {
        if (!gesture.IsValid) throw new ArgumentException("A shortcut requires a modifier and a main key.", nameof(gesture));
        Change(_data with { ShortcutModifiers = gesture.Modifiers, ShortcutVirtualKey = gesture.VirtualKey });
    }
    public void SetWindowPosition(int x, int y) => Change(_data with { WindowX = x, WindowY = y });
    public void SetWindowPosition(string monitorId, int x, int y)
    {
        var placements = new Dictionary<string, SavedWindowPlacement>(_data.MonitorPlacements, StringComparer.OrdinalIgnoreCase)
        {
            [monitorId] = new(x, y)
        };
        Change(_data with { WindowX = x, WindowY = y, LastMonitorId = monitorId, MonitorPlacements = placements });
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _saveCancellation?.Cancel();
        await _saveTask;
        if (store is not null)
        {
            try { await store.SaveAsync(_data); }
            catch (Exception ex) { Report(ex); }
        }
        _saveCancellation?.Dispose();
    }
}
