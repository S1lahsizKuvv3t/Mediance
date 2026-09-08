using System.ComponentModel;
using Mediance.AcrylicProbe.Localization;
using Mediance.AcrylicProbe.ViewModels;
using Mediance.AcrylicProbe.Windowing;
using Mediance.Core.Media;
using Mediance.Core.Settings;
using Mediance.Windows.Media;
using Mediance.Windows.Audio;
using Mediance.Windows.Windowing;
using Mediance.Lyrics;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Mediance.AcrylicProbe;

public sealed partial class MainWindow : Window
{
    private readonly WidgetFrame _frame;
    private readonly WindowDragBehavior _drag;
    private readonly WindowAppearance _appearance;
    private readonly SystemTrayIcon? _tray = null;
    private readonly HttpClient _lyricsClient;
    private SettingsWindow? _settingsWindow;
    private Storyboard? _lyricsTransition;
    private Storyboard? _ambientTransition;
    private Storyboard? _progressTransition;
    private Storyboard? _progressValueTransition;
    private Storyboard? _volumeHudTransition;
    private Storyboard? _entranceTransition;
    private Storyboard? _trackTransition;
    private Storyboard? _artworkTransition;
    private Storyboard? _lyricsPanelTransition;
    private CancellationTokenSource? _volumeHudCancellation;
    private ButtonBase? _pressedButton;
    private string? _lastTrackIdentity;
    private readonly bool _animationsEnabled = AreAnimationsEnabled();
    private readonly bool _smoke;
    private readonly StartupRegistration _startup = new(Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "Mediance.exe"));
    private bool _settingsLoaded;
    private bool _seeking;
    private uint _seekPointer;
    private bool _initialized;
    private bool _closing;
    private bool _allowClose;
    private bool _resizeQueued;
    private bool _lyricsToggleBusy;
    public PlayerViewModel Model { get; }
    public SettingsViewModel Settings { get; }
    public AudioRoutingViewModel Routing { get; }
    public LyricsViewModel Lyrics { get; }
    public HotkeyViewModel Hotkey { get; }

    public MainWindow()
    {
        _smoke = Environment.GetCommandLineArgs().Contains("--smoke-test");
        Model = new(new WindowsMediaSessionService(), new WindowsArtworkPaletteService(), DispatcherQueue);
        Routing = new(new WindowsAudioRoutingService(), Model);
        _lyricsClient = new();
        Lyrics = new(new LyricsService(new MemoryLyricsCacheProvider(new FallbackLyricsProvider(
            new TimeoutLyricsProvider(new LrcLibLyricsProvider(_lyricsClient), TimeSpan.FromSeconds(4)),
            new TimeoutLyricsProvider(new BetterLyricsProvider(_lyricsClient), TimeSpan.FromSeconds(3)),
            new TimeoutLyricsProvider(new AmllLyricsProvider(_lyricsClient), TimeSpan.FromSeconds(3)),
            new TimeoutLyricsProvider(new AppleMusicLyricsProvider(_lyricsClient), TimeSpan.FromSeconds(4)),
            new FirstAvailableLyricsProvider(
                new TimeoutLyricsProvider(new SarkiAnaliziLyricsProvider(_lyricsClient), TimeSpan.FromSeconds(3)),
                new TimeoutLyricsProvider(new SozMuzikLyricsProvider(_lyricsClient), TimeSpan.FromSeconds(3)),
                new TimeoutLyricsProvider(new GeniusLyricsProvider(_lyricsClient), TimeSpan.FromSeconds(3)),
                new TimeoutLyricsProvider(new BbsLyricsProvider(_lyricsClient), TimeSpan.FromSeconds(3))))),
            new LocalLyricsTimingStore(GetLyricsTimingPath()), TimeSpan.FromSeconds(20)), Model, DispatcherQueue);
        Lyrics.LinesChanged += Lyrics_LinesChanged;
        Settings = new(_smoke ? null : new JsonSettingsStore(GetSettingsPath()));
        InitializeComponent();
        Root.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(Interactive_PointerPressed), true);
        Root.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(Interactive_PointerReleased), true);
        Root.AddHandler(UIElement.PointerCanceledEvent, new PointerEventHandler(Interactive_PointerCanceled), true);
        Root.AddHandler(UIElement.PointerCaptureLostEvent, new PointerEventHandler(Interactive_PointerCanceled), true);
        Title = TextCatalog.Get("WindowTitle");
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Mediance.ico");
        if (File.Exists(iconPath)) AppWindow.SetIcon(iconPath);
        _frame = new(this);
        var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        Hotkey = new(new GlobalHotkeyRegistration(windowHandle), Settings);
        Hotkey.Pressed += Hotkey_Pressed;
        _drag = new(Surface, AppWindow, () => !Settings.IsLocked);
        _drag.Completed += Drag_Completed;
        _appearance = new(this, Surface);
        try
        {
            _tray = new(windowHandle, TextCatalog.AppName,
                new(TextCatalog.Get("ShowWidget"), TextCatalog.Get("HideWidget"),
                    TextCatalog.Settings, TextCatalog.Get("Exit")), () => _frame.IsVisible, iconPath);
            _tray.ToggleRequested += Tray_ToggleRequested;
            _tray.SettingsRequested += Tray_SettingsRequested;
            _tray.ExitRequested += Tray_ExitRequested;
        }
        catch (Exception ex)
        {
            ProbeLog.Write("Tray", ex);
            if (_smoke) throw;
        }
        _initialized = true;
        Model.PropertyChanged += Model_PropertyChanged;
        Settings.Changed += Settings_Changed;
        Settings_Changed(this, EventArgs.Empty);
        AppWindow.Closing += Window_Closing;
        Root.Loaded += Root_Loaded;
    }

    private static string GetSettingsPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var currentDirectory = Path.Combine(localAppData, "Mediance");
        var currentPath = Path.Combine(currentDirectory, "widget-settings.json");
        var legacyPath = Path.Combine(localAppData, "MediaHUB", "widget-settings.json");

        if (!File.Exists(currentPath) && File.Exists(legacyPath))
        {
            Directory.CreateDirectory(currentDirectory);
            File.Copy(legacyPath, currentPath);
        }

        return currentPath;
    }

    private static string GetLyricsTimingPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "Mediance", "lyrics-timing.json");
    }

    private async void Root_Loaded(object sender, RoutedEventArgs e)
    {
        Root.Loaded -= Root_Loaded;
        await Settings.LoadAsync();
        _settingsLoaded = true;
        ApplyStartupSetting();
        await Model.StartAsync();
        if (Settings.LyricsOpen) _ = Lyrics.SetVisibleAsync(true);
        await Routing.RefreshAsync();
        Root.UpdateLayout();
        _frame.ResizeContent(Settings.WindowWidth, Root.ActualHeight + 26);
        RestoreSavedPosition();
        QueueResize();
        if (Environment.GetCommandLineArgs().Contains("--background")) _frame.Hide();
        else AnimateEntrance();
        if (Environment.GetCommandLineArgs().Contains("--smoke-test"))
        {
            try { await RunSmokeAsync(); }
            catch (Exception ex) { ProbeLog.Write("WindowSmokeFailed", ex); Environment.ExitCode = 1; }
            await ShutdownAsync();
        }
    }

    private void Settings_Changed(object? sender, EventArgs e)
    {
        _appearance.Apply(Settings.Data);
        _frame.SetTopmost(Settings.AlwaysOnTop);
        Lyrics.Lead = TimeSpan.FromMilliseconds(Settings.LyricsLeadMilliseconds);
        if (_settingsLoaded) ApplyStartupSetting();
        QueueResize();
    }
    private void ApplyStartupSetting()
    {
        if (_smoke) return;
        try
        {
            if (_startup.IsEnabled != Settings.StartWithWindows) _startup.SetEnabled(Settings.StartWithWindows);
        }
        catch (Exception ex) { ProbeLog.Write("StartupRegistration", ex); }
    }
    private void RestoreSavedPosition()
    {
        var monitors = NativeWindowFeatures.WorkAreasWithIds();
        if (Settings.LastMonitorId is { } id &&
            Settings.MonitorPlacements.TryGetValue(id, out var saved) &&
            monitors.FirstOrDefault(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase)) is { } monitor)
        {
            _frame.RestorePosition(saved.X, saved.Y, monitor.WorkArea);
            return;
        }
        if (Settings.WindowX is { } x && Settings.WindowY is { } y) _frame.RestorePosition(x, y);
    }
    private void Root_SizeChanged(object sender, SizeChangedEventArgs e) => QueueResize();
    private void QueueResize()
    {
        if (!_initialized || _closing || _resizeQueued) return;
        _resizeQueued = true;
        DispatcherQueue.TryEnqueue(() =>
        {
            _resizeQueued = false;
            if (!_closing && Root.ActualHeight > 0) _frame.ResizeContent(Settings.WindowWidth, Root.ActualHeight + 26);
        });
    }

    private SettingsWindow OpenSettings()
    {
        if (_settingsWindow is null)
        {
            _settingsWindow = new(Settings, Routing, Hotkey, AppWindow);
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        }
        _settingsWindow.Activate();
        return _settingsWindow;
    }
    private void Settings_Click(object sender, RoutedEventArgs e) => OpenSettings();
    private void Drag_Completed(object? sender, EventArgs e) => SaveWindowPosition();
    private void SaveWindowPosition()
    {
        var center = new Mediance.Core.Windowing.PixelPoint(
            AppWindow.Position.X + AppWindow.Size.Width / 2,
            AppWindow.Position.Y + AppWindow.Size.Height / 2);
        Settings.SetWindowPosition(NativeWindowFeatures.MonitorIdAt(center), AppWindow.Position.X, AppWindow.Position.Y);
    }

    internal void ShowAndActivate()
    {
        if (_closing) return;
        _frame.ShowAndActivate(this);
        AnimateEntrance();
    }
    internal void HideForBackgroundStartup() => _frame.Hide();
    private async void Previous_Click(object sender, RoutedEventArgs e) => await Model.SendAsync(MediaCommand.Previous);
    private async void Play_Click(object sender, RoutedEventArgs e) => await Model.SendAsync(MediaCommand.Toggle);
    private async void Next_Click(object sender, RoutedEventArgs e) => await Model.SendAsync(MediaCommand.Next);
    private async void RefreshAudio_Click(object sender, RoutedEventArgs e) => await Routing.RefreshAsync();
    private async void Lyrics_Click(object sender, RoutedEventArgs e)
    {
        if (_lyricsToggleBusy) return;
        _lyricsToggleBusy = true;
        try
        {
            if (LyricsPanel.Visibility == Visibility.Visible)
            {
                await AnimateLyricsPanelHideAsync();
                await Lyrics.ToggleAsync();
                Settings.LyricsOpen = Lyrics.IsVisible;
                return;
            }
            await Lyrics.ToggleAsync();
            Settings.LyricsOpen = Lyrics.IsVisible;
            if (LyricsPanel.Visibility == Visibility.Visible) AnimateLyricsPanelReveal();
        }
        finally { _lyricsToggleBusy = false; }
    }
    private async void PreviousLyric_Click(object sender, RoutedEventArgs e) => await Lyrics.SeekVisibleLineAsync(-1);
    private async void CurrentLyric_Click(object sender, RoutedEventArgs e) => await Lyrics.SeekVisibleLineAsync(0);
    private async void NextLyric_Click(object sender, RoutedEventArgs e) => await Lyrics.SeekVisibleLineAsync(1);
    private async void LyricsSync_Click(object sender, RoutedEventArgs e) => await Lyrics.StartOrMarkAuthoringAsync();
    private async void LyricsSyncRestart_Click(object sender, RoutedEventArgs e) => await Lyrics.RestartAuthoringAsync();
    private async void LyricsSyncRedo_Click(object sender, RoutedEventArgs e) => await Lyrics.BeginRetimingAsync();
    private void LyricsSyncCancel_Click(object sender, RoutedEventArgs e) => Lyrics.CancelAuthoring();
    private async void LyricsSyncSpace_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (!Lyrics.IsAuthoring) return;
        args.Handled = true;
        await Lyrics.StartOrMarkAuthoringAsync();
    }
    private void LyricsPanel_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var delta = e.GetCurrentPoint(LyricsPanel).Properties.MouseWheelDelta;
        if (Lyrics.Scroll(delta)) e.Handled = true;
    }

    private void Model_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null or nameof(PlayerViewModel.AmbientColor)) AnimateAmbientGlow(Model.AmbientColor);
        if (!_seeking && (e.PropertyName is null or nameof(PlayerViewModel.Progress))) UpdateProgressVisual();
        if (e.PropertyName == nameof(PlayerViewModel.Artwork)) AnimateArtworkReveal();
        if (e.PropertyName is null && !string.Equals(_lastTrackIdentity, Model.TrackIdentity, StringComparison.Ordinal))
        {
            _lastTrackIdentity = Model.TrackIdentity;
            if (Root.IsLoaded) AnimateTrackChange();
        }
    }

    private static bool AreAnimationsEnabled()
    {
        try { return new UISettings().AnimationsEnabled; }
        catch { return true; }
    }

    private void AnimateEntrance()
    {
        _entranceTransition?.Stop();
        if (!_animationsEnabled)
        {
            Surface.Opacity = 1;
            RootTransform.ScaleX = RootTransform.ScaleY = 1;
            RootTransform.TranslateY = 0;
            return;
        }
        Surface.Opacity = 0;
        RootTransform.ScaleX = RootTransform.ScaleY = 0.965;
        RootTransform.TranslateY = 12;
        var storyboard = new Storyboard();
        storyboard.Children.Add(Animation(Surface, "Opacity", 0, 1, 260, new SineEase { EasingMode = EasingMode.EaseOut }));
        storyboard.Children.Add(Animation(RootTransform, "ScaleX", 0.965, 1, 440, new QuinticEase { EasingMode = EasingMode.EaseOut }));
        storyboard.Children.Add(Animation(RootTransform, "ScaleY", 0.965, 1, 440, new QuinticEase { EasingMode = EasingMode.EaseOut }));
        storyboard.Children.Add(Animation(RootTransform, "TranslateY", 12, 0, 480, new QuinticEase { EasingMode = EasingMode.EaseOut }));
        storyboard.Completed += (_, _) =>
        {
            if (!ReferenceEquals(_entranceTransition, storyboard)) return;
            Surface.Opacity = 1;
            RootTransform.ScaleX = RootTransform.ScaleY = 1;
            RootTransform.TranslateY = 0;
            storyboard.Stop();
            _entranceTransition = null;
        };
        _entranceTransition = storyboard;
        storyboard.Begin();
    }

    private void AnimateTrackChange()
    {
        _trackTransition?.Stop();
        if (!_animationsEnabled || TrackPanel.Visibility != Visibility.Visible)
        {
            TrackPanel.Opacity = 1;
            TrackPanelTransform.TranslateY = 0;
            TrackPanelTransform.ScaleX = TrackPanelTransform.ScaleY = 1;
            return;
        }
        TrackPanel.Opacity = 0.28;
        TrackPanelTransform.TranslateY = 8;
        TrackPanelTransform.ScaleX = TrackPanelTransform.ScaleY = 0.975;
        var storyboard = new Storyboard();
        var ease = new QuinticEase { EasingMode = EasingMode.EaseOut };
        storyboard.Children.Add(Animation(TrackPanel, "Opacity", 0.28, 1, 280, ease));
        storyboard.Children.Add(Animation(TrackPanelTransform, "TranslateY", 8, 0, 460, ease));
        storyboard.Children.Add(Animation(TrackPanelTransform, "ScaleX", 0.975, 1, 520, ease));
        storyboard.Children.Add(Animation(TrackPanelTransform, "ScaleY", 0.975, 1, 520, ease));
        storyboard.Completed += (_, _) =>
        {
            if (!ReferenceEquals(_trackTransition, storyboard)) return;
            TrackPanel.Opacity = 1;
            TrackPanelTransform.TranslateY = 0;
            TrackPanelTransform.ScaleX = TrackPanelTransform.ScaleY = 1;
            storyboard.Stop();
            _trackTransition = null;
        };
        _trackTransition = storyboard;
        storyboard.Begin();
    }

    private void AnimateLyricsPanelReveal()
    {
        _lyricsPanelTransition?.Stop();
        if (!_animationsEnabled)
        {
            LyricsPanel.Opacity = 1;
            LyricsPanelTransform.ScaleX = LyricsPanelTransform.ScaleY = 1;
            LyricsPanelTransform.TranslateY = 0;
            return;
        }
        LyricsPanel.Opacity = 0;
        LyricsPanelTransform.ScaleX = LyricsPanelTransform.ScaleY = 0.955;
        LyricsPanelTransform.TranslateY = 18;
        var storyboard = new Storyboard();
        var ease = new QuinticEase { EasingMode = EasingMode.EaseOut };
        var settle = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.14 };
        storyboard.Children.Add(Animation(LyricsPanel, "Opacity", 0, 1, 240, ease));
        storyboard.Children.Add(Animation(LyricsPanelTransform, "ScaleX", 0.955, 1, 440, settle));
        storyboard.Children.Add(Animation(LyricsPanelTransform, "ScaleY", 0.955, 1, 440, settle));
        storyboard.Children.Add(Animation(LyricsPanelTransform, "TranslateY", 18, 0, 460, ease));
        storyboard.Completed += (_, _) =>
        {
            if (!ReferenceEquals(_lyricsPanelTransition, storyboard)) return;
            LyricsPanel.Opacity = 1;
            LyricsPanelTransform.ScaleX = LyricsPanelTransform.ScaleY = 1;
            LyricsPanelTransform.TranslateY = 0;
            storyboard.Stop();
            _lyricsPanelTransition = null;
        };
        _lyricsPanelTransition = storyboard;
        storyboard.Begin();
    }

    private Task AnimateLyricsPanelHideAsync()
    {
        _lyricsPanelTransition?.Stop();
        if (!_animationsEnabled)
        {
            LyricsPanel.Opacity = 1;
            LyricsPanelTransform.ScaleX = LyricsPanelTransform.ScaleY = 1;
            LyricsPanelTransform.TranslateY = 0;
            return Task.CompletedTask;
        }
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var storyboard = new Storyboard();
        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
        storyboard.Children.Add(Animation(LyricsPanel, "Opacity", LyricsPanel.Opacity, 0, 160, ease));
        storyboard.Children.Add(Animation(LyricsPanelTransform, "ScaleX", LyricsPanelTransform.ScaleX, 0.975, 190, ease));
        storyboard.Children.Add(Animation(LyricsPanelTransform, "ScaleY", LyricsPanelTransform.ScaleY, 0.975, 190, ease));
        storyboard.Children.Add(Animation(LyricsPanelTransform, "TranslateY", LyricsPanelTransform.TranslateY, 10, 190, ease));
        storyboard.Completed += (_, _) =>
        {
            if (ReferenceEquals(_lyricsPanelTransition, storyboard)) _lyricsPanelTransition = null;
            storyboard.Stop();
            LyricsPanel.Opacity = 1;
            LyricsPanelTransform.ScaleX = LyricsPanelTransform.ScaleY = 1;
            LyricsPanelTransform.TranslateY = 0;
            completion.TrySetResult();
        };
        _lyricsPanelTransition = storyboard;
        storyboard.Begin();
        return completion.Task;
    }

    private void AnimateArtworkReveal()
    {
        _artworkTransition?.Stop();
        if (!_animationsEnabled || ArtworkCard.Visibility != Visibility.Visible)
        {
            ArtworkCard.Opacity = 1;
            ArtworkTransform.ScaleX = ArtworkTransform.ScaleY = 1;
            return;
        }
        ArtworkCard.Opacity = 0.16;
        ArtworkTransform.ScaleX = ArtworkTransform.ScaleY = 0.92;
        var storyboard = new Storyboard();
        var ease = new QuinticEase { EasingMode = EasingMode.EaseOut };
        storyboard.Children.Add(Animation(ArtworkCard, "Opacity", 0.16, 1, 260, ease));
        storyboard.Children.Add(Animation(ArtworkTransform, "ScaleX", 0.92, 1, 480, ease));
        storyboard.Children.Add(Animation(ArtworkTransform, "ScaleY", 0.92, 1, 480, ease));
        storyboard.Completed += (_, _) =>
        {
            if (!ReferenceEquals(_artworkTransition, storyboard)) return;
            ArtworkCard.Opacity = 1;
            ArtworkTransform.ScaleX = ArtworkTransform.ScaleY = 1;
            storyboard.Stop();
            _artworkTransition = null;
        };
        _artworkTransition = storyboard;
        storyboard.Begin();
    }

    private void AnimateAmbientGlow(Color color)
    {
        if (_closing || AmbientColorStop.Color == color) return;
        _ambientTransition?.SkipToFill();
        _ambientTransition?.Stop();
        var storyboard = new Storyboard();
        var animation = new ColorAnimation
        {
            From = AmbientColorStop.Color,
            To = color,
            Duration = new Duration(TimeSpan.FromMilliseconds(_animationsEnabled ? 1100 : 1)),
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        Storyboard.SetTarget(animation, AmbientColorStop);
        Storyboard.SetTargetProperty(animation, "Color");
        storyboard.Children.Add(animation);
        if (_animationsEnabled)
        {
            AmbientGlow.Opacity = 0.12;
            AmbientGlowTransform.ScaleX = AmbientGlowTransform.ScaleY = 1.06;
            storyboard.Children.Add(Animation(AmbientGlow, "Opacity", 0.12, 0.2, 900, new SineEase { EasingMode = EasingMode.EaseOut }));
            storyboard.Children.Add(Animation(AmbientGlowTransform, "ScaleX", 1.06, 1, 1150, new QuinticEase { EasingMode = EasingMode.EaseOut }));
            storyboard.Children.Add(Animation(AmbientGlowTransform, "ScaleY", 1.06, 1, 1150, new QuinticEase { EasingMode = EasingMode.EaseOut }));
        }
        storyboard.Completed += (_, _) =>
        {
            if (!ReferenceEquals(_ambientTransition, storyboard)) return;
            AmbientColorStop.Color = color;
            AmbientGlow.Opacity = 0.2;
            AmbientGlowTransform.ScaleX = AmbientGlowTransform.ScaleY = 1;
            storyboard.Stop();
            _ambientTransition = null;
        };
        _ambientTransition = storyboard;
        storyboard.Begin();
    }

    private void SeekTrack_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateProgressVisual();
    private void UpdateProgressVisual(double? preview = null)
    {
        if (ProgressRail.ActualWidth <= 0) return;
        var target = Math.Clamp(preview ?? Model.Progress, 0, 1);
        _progressValueTransition?.Stop();
        if (preview.HasValue || !_animationsEnabled)
        {
            ProgressScale.ScaleX = target;
            _progressValueTransition = null;
            return;
        }
        var start = ProgressScale.ScaleX;
        if (Math.Abs(target - start) < 0.0001) return;
        var storyboard = new Storyboard();
        storyboard.Children.Add(Animation(ProgressScale, "ScaleX", start, target, 180,
            new SineEase { EasingMode = EasingMode.EaseOut }));
        storyboard.Completed += (_, _) =>
        {
            if (!ReferenceEquals(_progressValueTransition, storyboard)) return;
            ProgressScale.ScaleX = target;
            storyboard.Stop();
            _progressValueTransition = null;
        };
        _progressValueTransition = storyboard;
        storyboard.Begin();
    }
    private void SeekTrack_PointerEntered(object sender, PointerRoutedEventArgs e) => AnimateProgressHeight(6);
    private void SeekTrack_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (!_seeking) AnimateProgressHeight(3);
    }
    private void AnimateProgressHeight(double height)
    {
        _progressTransition?.Stop();
        var storyboard = new Storyboard();
        storyboard.Children.Add(Animation(ProgressRail, "Height", ProgressRail.ActualHeight, height, 120));
        storyboard.Completed += (_, _) =>
        {
            if (!ReferenceEquals(_progressTransition, storyboard)) return;
            ProgressRail.Height = height;
            storyboard.Stop();
            _progressTransition = null;
        };
        _progressTransition = storyboard;
        storyboard.Begin();
    }
    private void SeekTrack_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!Model.CanSeek || !e.GetCurrentPoint(SeekTrack).Properties.IsLeftButtonPressed ||
            !SeekTrack.CapturePointer(e.Pointer)) return;
        _seeking = true;
        _seekPointer = e.Pointer.PointerId;
        AnimateProgressHeight(6);
        UpdateProgressVisual(SeekPercentage(e));
        e.Handled = true;
    }
    private void SeekTrack_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_seeking || e.Pointer.PointerId != _seekPointer) return;
        if (!e.GetCurrentPoint(SeekTrack).Properties.IsLeftButtonPressed)
        {
            EndSeek(e);
            return;
        }
        UpdateProgressVisual(SeekPercentage(e));
        e.Handled = true;
    }
    private async void SeekTrack_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_seeking || e.Pointer.PointerId != _seekPointer) return;
        var percentage = SeekPercentage(e);
        EndSeek(e);
        e.Handled = true;
        await Model.SeekToPercentageAsync(percentage);
        UpdateProgressVisual();
    }
    private void SeekTrack_PointerCanceled(object sender, PointerRoutedEventArgs e) => EndSeek(e);
    private void SeekTrack_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        if (!_seeking || e.Pointer.PointerId != _seekPointer) return;
        _seeking = false;
        UpdateProgressVisual();
        AnimateProgressHeight(3);
    }
    private double SeekPercentage(PointerRoutedEventArgs e) => SeekTrack.ActualWidth <= 0 ? 0 :
        Math.Clamp(e.GetCurrentPoint(SeekTrack).Position.X / SeekTrack.ActualWidth, 0, 1);
    private void EndSeek(PointerRoutedEventArgs e)
    {
        if (!_seeking || e.Pointer.PointerId != _seekPointer) return;
        _seeking = false;
        SeekTrack.ReleasePointerCapture(e.Pointer);
        AnimateProgressHeight(3);
    }

    private async void Surface_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (_closing || !Settings.EnableWheelVolume) return;
        if (IsDescendantOf(e.OriginalSource as DependencyObject, LyricsPanel)) return;
        var delta = e.GetCurrentPoint(Surface).Properties.MouseWheelDelta;
        if (delta == 0) return;
        e.Handled = true;
        var result = await Routing.AdjustVolumeAsync(Math.Sign(delta));
        if (result?.Succeeded == true) ShowVolumeHud(result.Percent, result.IsMuted);
    }

    private static bool IsDescendantOf(DependencyObject? source, DependencyObject ancestor)
    {
        for (var current = source; current is not null; current = VisualTreeHelper.GetParent(current))
            if (ReferenceEquals(current, ancestor)) return true;
        return false;
    }

    private void ShowVolumeHud(int percent, bool muted)
    {
        _volumeHudCancellation?.Cancel();
        _volumeHudCancellation?.Dispose();
        _volumeHudCancellation = new();
        _volumeHudTransition?.Stop();
        VolumeHudText.Text = $"{(muted ? "🔇" : "🔊")}  {percent}%";
        VolumeHud.Visibility = Visibility.Visible;
        VolumeHud.Opacity = _animationsEnabled ? 0 : 1;
        VolumeHudTransform.ScaleX = VolumeHudTransform.ScaleY = _animationsEnabled ? 0.86 : 1;
        VolumeHudTransform.TranslateY = _animationsEnabled ? -6 : 0;
        if (_animationsEnabled)
        {
            var storyboard = new Storyboard();
            var ease = new QuinticEase { EasingMode = EasingMode.EaseOut };
            storyboard.Children.Add(Animation(VolumeHud, "Opacity", 0, 1, 140, ease));
            storyboard.Children.Add(Animation(VolumeHudTransform, "ScaleX", 0.86, 1, 240, ease));
            storyboard.Children.Add(Animation(VolumeHudTransform, "ScaleY", 0.86, 1, 240, ease));
            storyboard.Children.Add(Animation(VolumeHudTransform, "TranslateY", -6, 0, 260, ease));
            _volumeHudTransition = storyboard;
            storyboard.Begin();
        }
        _ = HideVolumeHudAsync(_volumeHudCancellation);
    }
    private async Task HideVolumeHudAsync(CancellationTokenSource owner)
    {
        try { await Task.Delay(1200, owner.Token); }
        catch (OperationCanceledException) { return; }
        if (_closing || !ReferenceEquals(owner, _volumeHudCancellation)) return;
        var storyboard = new Storyboard();
        storyboard.Children.Add(Animation(VolumeHud, "Opacity", 1, 0, _animationsEnabled ? 220 : 1));
        if (_animationsEnabled)
        {
            storyboard.Children.Add(Animation(VolumeHudTransform, "ScaleX", 1, 0.96, 220));
            storyboard.Children.Add(Animation(VolumeHudTransform, "ScaleY", 1, 0.96, 220));
            storyboard.Children.Add(Animation(VolumeHudTransform, "TranslateY", 0, -5, 220));
        }
        storyboard.Completed += (_, _) =>
        {
            if (!ReferenceEquals(_volumeHudTransition, storyboard) ||
                !ReferenceEquals(owner, _volumeHudCancellation)) return;
            storyboard.Stop();
            VolumeHud.Visibility = Visibility.Collapsed;
            VolumeHudTransform.ScaleX = VolumeHudTransform.ScaleY = 1;
            VolumeHudTransform.TranslateY = 0;
            _volumeHudTransition = null;
        };
        _volumeHudTransition = storyboard;
        storyboard.Begin();
    }

    private void Hotkey_Pressed(object? sender, EventArgs e) => ToggleWindowVisibility();

    private void Tray_ToggleRequested(object? sender, EventArgs e) => ToggleWindowVisibility();
    private void Tray_SettingsRequested(object? sender, EventArgs e)
    {
        if (!_frame.IsVisible) _frame.ShowWithoutActivation();
        OpenSettings();
    }
    private async void Tray_ExitRequested(object? sender, EventArgs e) => await ShutdownAsync();
    private void ToggleWindowVisibility()
    {
        if (_frame.IsVisible)
        {
            _settingsWindow?.Close();
            _frame.Hide();
        }
        else _frame.ShowWithoutActivation();
        if (_frame.IsVisible) AnimateEntrance();
    }

    private void HideToTray()
    {
        _settingsWindow?.Close();
        _frame.Hide();
    }

    private void Lyrics_LinesChanged(object? sender, LyricsLinesChangedEventArgs args) =>
        DispatcherQueue.TryEnqueue(() => AnimateLyrics(args));

    private void AnimateLyrics(LyricsLinesChangedEventArgs args)
    {
        if (_closing) return;
        _lyricsTransition?.Stop();
        OutgoingPreviousLine.Text = args.PreviousLine;
        OutgoingCurrentLine.Text = args.CurrentLine;
        OutgoingNextLine.Text = args.NextLine;
        var hasOutgoing = !string.IsNullOrEmpty(args.PreviousLine) ||
                          !string.IsNullOrEmpty(args.CurrentLine) ||
                          !string.IsNullOrEmpty(args.NextLine);
        OutgoingLyricsLines.Visibility = hasOutgoing ? Visibility.Visible : Visibility.Collapsed;
        OutgoingLyricsLines.Opacity = hasOutgoing ? 1 : 0;
        OutgoingLyricsTranslate.Y = 0;
        SyncedLyricsLines.Opacity = _animationsEnabled ? 0 : 1;
        SyncedLyricsTranslate.Y = _animationsEnabled ? (args.Forward ? 18 : -18) : 0;

        var storyboard = new Storyboard();
        if (hasOutgoing)
        {
            storyboard.Children.Add(Animation(OutgoingLyricsLines, "Opacity", 1, 0, _animationsEnabled ? 115 : 1));
            storyboard.Children.Add(Animation(OutgoingLyricsTranslate, "Y", 0, args.Forward ? -12 : 12, _animationsEnabled ? 190 : 1));
        }
        storyboard.Children.Add(Animation(SyncedLyricsLines, "Opacity", _animationsEnabled ? 0 : 1, 1, _animationsEnabled ? 230 : 1,
            new SineEase { EasingMode = EasingMode.EaseOut }, _animationsEnabled ? 90 : 0));
        storyboard.Children.Add(Animation(SyncedLyricsTranslate, "Y", _animationsEnabled ? (args.Forward ? 18 : -18) : 0, 0, _animationsEnabled ? 380 : 1,
            new QuinticEase { EasingMode = EasingMode.EaseOut }, _animationsEnabled ? 45 : 0));
        storyboard.Completed += (_, _) =>
        {
            if (!ReferenceEquals(_lyricsTransition, storyboard)) return;
            storyboard.Stop();
            SyncedLyricsLines.Opacity = 1;
            SyncedLyricsTranslate.Y = 0;
            OutgoingLyricsLines.Opacity = 0;
            OutgoingLyricsTranslate.Y = 0;
            OutgoingLyricsLines.Visibility = Visibility.Collapsed;
            _lyricsTransition = null;
        };
        _lyricsTransition = storyboard;
        storyboard.Begin();
    }

    private static DoubleAnimation Animation(DependencyObject target, string property,
        double from, double to, int milliseconds, EasingFunctionBase? easing = null, int delayMilliseconds = 0)
    {
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            BeginTime = TimeSpan.FromMilliseconds(delayMilliseconds),
            Duration = new Duration(TimeSpan.FromMilliseconds(milliseconds)),
            EasingFunction = easing ?? new CubicEase { EasingMode = EasingMode.EaseOut },
            EnableDependentAnimation = property is "Height" or "Width"
        };
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, property);
        return animation;
    }

    private void Interactive_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _pressedButton = FindButton(e.OriginalSource as DependencyObject);
        if (_pressedButton is not null) AnimateButton(_pressedButton, 0.91, 85);
    }

    private void Interactive_PointerReleased(object sender, PointerRoutedEventArgs e) => ReleasePressedButton();
    private void Interactive_PointerCanceled(object sender, PointerRoutedEventArgs e) => ReleasePressedButton();

    private void ReleasePressedButton()
    {
        if (_pressedButton is null) return;
        AnimateButton(_pressedButton, 1, 260);
        _pressedButton = null;
    }

    private static ButtonBase? FindButton(DependencyObject? source)
    {
        for (var current = source; current is not null; current = VisualTreeHelper.GetParent(current))
            if (current is ButtonBase button) return button;
        return null;
    }

    private void AnimateButton(ButtonBase button, double scale, int milliseconds)
    {
        if (!_animationsEnabled) return;
        button.RenderTransformOrigin = new global::Windows.Foundation.Point(0.5, 0.5);
        if (button.RenderTransform is not CompositeTransform transform)
        {
            transform = new CompositeTransform();
            button.RenderTransform = transform;
        }
        var storyboard = new Storyboard();
        EasingFunctionBase ease = scale < 1
            ? new CubicEase { EasingMode = EasingMode.EaseOut }
            : new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.12 };
        storyboard.Children.Add(Animation(transform, "ScaleX", transform.ScaleX, scale, milliseconds, ease));
        storyboard.Children.Add(Animation(transform, "ScaleY", transform.ScaleY, scale, milliseconds, ease));
        storyboard.Begin();
    }
    private async void Close_Click(object sender, RoutedEventArgs e)
    {
        if (Settings.CloseToTray) HideToTray();
        else await ShutdownAsync();
    }

    private async void Window_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_allowClose) return;
        args.Cancel = true;
        if (Settings.CloseToTray) HideToTray();
        else await ShutdownAsync();
    }
    private async Task ShutdownAsync()
    {
        if (_closing) return;
        _closing = true;
        SaveWindowPosition();
        Settings.LyricsOpen = Lyrics.IsVisible;
        _settingsWindow?.Close();
        Settings.Changed -= Settings_Changed;
        Model.PropertyChanged -= Model_PropertyChanged;
        Lyrics.LinesChanged -= Lyrics_LinesChanged;
        _lyricsTransition?.Stop();
        _ambientTransition?.Stop();
        _progressTransition?.Stop();
        _progressValueTransition?.Stop();
        _volumeHudTransition?.Stop();
        _entranceTransition?.Stop();
        _trackTransition?.Stop();
        _artworkTransition?.Stop();
        _lyricsPanelTransition?.Stop();
        _volumeHudCancellation?.Cancel();
        Hotkey.Pressed -= Hotkey_Pressed;
        if (_tray is not null)
        {
            _tray.ToggleRequested -= Tray_ToggleRequested;
            _tray.SettingsRequested -= Tray_SettingsRequested;
            _tray.ExitRequested -= Tray_ExitRequested;
            _tray.Dispose();
        }
        Hotkey.Dispose();
        _drag.Completed -= Drag_Completed;
        _drag.Dispose();
        Routing.Dispose();
        Lyrics.Dispose();
        _lyricsClient.Dispose();
        await Model.DisposeAsync();
        await Settings.DisposeAsync();
        _appearance.Dispose();
        _volumeHudCancellation?.Dispose();
        _allowClose = true;
        ProbeLog.Write("Closed");
        Close();
    }

    private async Task RunSmokeAsync()
    {
        await Task.Delay(200);
        await _frame.VerifyAsync();
        var workAreas = NativeWindowFeatures.WorkAreas();
        var identifiedWorkAreas = NativeWindowFeatures.WorkAreasWithIds();
        if (workAreas.Count == 0) throw new InvalidOperationException("No monitor work areas were enumerated.");
        if (identifiedWorkAreas.Count != workAreas.Count || identifiedWorkAreas.Any(x => string.IsNullOrWhiteSpace(x.Id)) ||
            identifiedWorkAreas.Select(x => x.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != identifiedWorkAreas.Count)
            throw new InvalidOperationException("Monitor identities were missing or duplicated.");
        foreach (var area in workAreas)
        {
            var center = new Mediance.Core.Windowing.PixelPoint(area.X + area.Width / 2, area.Y + area.Height / 2);
            if (NativeWindowFeatures.WorkAreaAt(center) != area)
                throw new InvalidOperationException("A monitor center resolved to the wrong work area.");
        }
        ProbeLog.Write($"WindowSmoke: resolved {workAreas.Count} monitor work area(s)");
        if (_tray?.IsAdded != true) throw new InvalidOperationException("System tray icon was not registered.");
        Settings.IsLocked = true;
        Settings.AlwaysOnTop = true;
        await Task.Delay(80);
        if (LockToggle.IsChecked is not true || TopmostToggle.IsChecked is not true ||
            !NativeWindowFeatures.IsTopmost(WinRT.Interop.WindowNative.GetWindowHandle(this)))
            throw new InvalidOperationException("Footer lock/topmost quick controls did not reflect settings.");
        Settings.Reset();
        if (!Hotkey.IsRegistered) throw new InvalidOperationException("Global hotkey registration failed.");
        var diagnosticGesture = new HotkeyGesture(
            HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift, 0x87); // F24
        Hotkey.BeginCapture();
        Hotkey.Capture(diagnosticGesture.VirtualKey, diagnosticGesture.Modifiers);
        if (!Hotkey.IsRegistered || Settings.Data.ShortcutVirtualKey != diagnosticGesture.VirtualKey)
            throw new InvalidOperationException("Hotkey capture/re-registration failed.");
        using (var duplicate = new GlobalHotkeyRegistration(WinRT.Interop.WindowNative.GetWindowHandle(this)))
        {
            if (duplicate.TryRegister(diagnosticGesture))
                throw new InvalidOperationException("Duplicate hotkey registration was not rejected.");
        }
        Settings.Reset();
        if (!Hotkey.IsRegistered) throw new InvalidOperationException("Default hotkey was not restored.");
        Hotkey_Pressed(this, EventArgs.Empty);
        await Task.Delay(80);
        if (_frame.IsVisible) throw new InvalidOperationException("Hotkey hide behavior failed.");
        Hotkey_Pressed(this, EventArgs.Empty);
        await Task.Delay(80);
        if (!_frame.IsVisible) throw new InvalidOperationException("Hotkey no-activate restore behavior failed.");
        ProbeLog.Write($"AcrylicNativeState: {_appearance.NativeState}");
        AnimateLyrics(new("Earlier", "Current", "Next", true));
        await Task.Delay(60);
        AnimateLyrics(new("Later", "Current", "Earlier", false));
        await Task.Delay(500);
        if (OutgoingLyricsLines.Visibility != Visibility.Collapsed ||
            Math.Abs(SyncedLyricsLines.Opacity - 1) > 0.01 || Math.Abs(SyncedLyricsTranslate.Y) > 0.01)
            throw new InvalidOperationException("Lyrics transition did not settle after interruption.");
        AnimateArtworkReveal();
        await Task.Delay(520);
        if (Math.Abs(ArtworkCard.Opacity - 1) > 0.01 ||
            Math.Abs(ArtworkTransform.ScaleX - 1) > 0.01 || Math.Abs(ArtworkTransform.ScaleY - 1) > 0.01)
            throw new InvalidOperationException("Artwork reveal did not settle.");
        Settings.GlassIntensity = 10;
        var lowAlpha = _appearance.OverlayAlpha;
        Settings.GlassIntensity = 95;
        if (_appearance.OverlayAlpha - lowAlpha < 200) throw new InvalidOperationException("Density did not change the rendered tint layer.");
        Settings.SolidBackground = true;
        if (!_appearance.IsSolid) throw new InvalidOperationException("Solid mode failed.");
        Settings.SolidBackground = false;
        if (_appearance.IsSolid) throw new InvalidOperationException("Acrylic reconnection failed.");
        Settings.Theme = ThemePreset.Prism;
        if (Surface.BorderBrush is not SolidColorBrush prismBorder || prismBorder.Color.B <= prismBorder.Color.R)
            throw new InvalidOperationException("Prism theme did not apply its accent border.");
        Settings.LyricsLineCount = 2;
        await Task.Delay(80);
        if (PreviousLyricButton.Visibility != Visibility.Collapsed || OutgoingPreviousLine.Visibility != Visibility.Collapsed)
            throw new InvalidOperationException("Two-line lyrics mode retained the previous line.");
        Settings.LyricsLineCount = 3;
        await Task.Delay(80);
        if (PreviousLyricButton.Visibility != Visibility.Visible || OutgoingPreviousLine.Visibility != Visibility.Visible)
            throw new InvalidOperationException("Three-line lyrics mode did not restore the previous line.");
        Settings.Reset();
        Settings.ShowProgress = false;
        Settings.EnableAmbientGlow = false;
        await Task.Delay(80);
        if (ProgressHost.Visibility != Visibility.Collapsed || AmbientGlow.Visibility != Visibility.Collapsed)
            throw new InvalidOperationException("Phase 2 visual settings did not collapse their elements.");
        ShowVolumeHud(75, false);
        if (VolumeHud.Visibility != Visibility.Visible || !VolumeHudText.Text.Contains("75%", StringComparison.Ordinal))
            throw new InvalidOperationException("Application volume HUD did not appear.");
        await Task.Delay(1600);
        if (VolumeHud.Visibility != Visibility.Collapsed)
            throw new InvalidOperationException("Application volume HUD did not fade out.");
        Settings.Reset();
        await Task.Delay(150);
        var expandedHeight = AppWindow.ClientSize.Height;
        Settings.ShowArtwork = false;
        Settings.ShowControls = false;
        Settings.ShowAudioOutput = false;
        await Task.Delay(200);
        if (ArtworkCard.Visibility != Visibility.Collapsed || ArtworkColumn.ActualWidth != 0 ||
            TransportControls.Visibility != Visibility.Collapsed || AudioOutputPanel.Visibility != Visibility.Collapsed ||
            AppWindow.ClientSize.Height >= expandedHeight)
            throw new InvalidOperationException("Hidden components did not collapse their layout.");
        Settings.ShowTitle = Settings.ShowArtist = Settings.ShowSource = Settings.ShowBrand = Settings.ShowCloseButton = false;
        await Task.Delay(150);
        if (SettingsButton.Visibility != Visibility.Visible || SettingsButton.ActualWidth <= 0)
            throw new InvalidOperationException("Settings access was lost with content hidden.");
        Settings.Reset();
        var settingsWindow = OpenSettings();
        if (!ReferenceEquals(settingsWindow, OpenSettings())) throw new InvalidOperationException("Duplicate settings window.");
        ProbeLog.Write("WindowSmoke: density, visibility, reflow and settings singleton passed");

        var previewArg = Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith("--preview-dir=", StringComparison.Ordinal));
        if (previewArg is not null)
        {
            var directory = previewArg["--preview-dir=".Length..];
            Settings.SolidBackground = true;
            if (Environment.GetCommandLineArgs().Contains("--narrow-preview")) Settings.WindowWidth = 420;
            await Task.Delay(300);
            await WindowPreview.SaveAsync(Surface, Path.Combine(directory, "widget.png"));
            if (Environment.GetCommandLineArgs().Contains("--lyrics-preview"))
            {
                await Lyrics.ToggleAsync();
                await Task.Delay(250);
                await WindowPreview.SaveAsync(Surface, Path.Combine(directory, "widget-lyrics.png"));
                await Lyrics.ToggleAsync();
            }
            await settingsWindow.SavePreviewAsync(Path.Combine(directory, "settings-appearance.png"), 0);
            await settingsWindow.SavePreviewAsync(Path.Combine(directory, "settings-elements.png"), 1);
            await settingsWindow.SavePreviewAsync(Path.Combine(directory, "settings-sizes.png"), 2);
            await settingsWindow.SavePreviewAsync(Path.Combine(directory, "settings-audio.png"), 3);
            Settings.ShowArtwork = false;
            Settings.ShowControls = false;
            Settings.ShowAudioOutput = false;
            await Task.Delay(150);
            await WindowPreview.SaveAsync(Surface, Path.Combine(directory, "widget-minimal.png"));
            Settings.Reset();
        }
        settingsWindow.Close();
        await Task.Delay(100);
        var reopened = OpenSettings();
        if (ReferenceEquals(reopened, settingsWindow)) throw new InvalidOperationException("Closed settings window was reused.");
        reopened.Close();
        ProbeLog.Write("WindowSmoke: settings reopen passed");
    }
}
