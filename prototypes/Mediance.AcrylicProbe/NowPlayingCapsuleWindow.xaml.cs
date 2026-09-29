using System.ComponentModel;
using Mediance.AcrylicProbe.ViewModels;
using Mediance.AcrylicProbe.Windowing;
using Mediance.Core.Media;
using Mediance.Core.Windowing;
using Mediance.Windows.Windowing;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Graphics;
using Windows.UI.ViewManagement;

namespace Mediance.AcrylicProbe;

public sealed partial class NowPlayingCapsuleWindow : Window, IDisposable
{
    private const double HeightDip = 34;
    private readonly SettingsViewModel _settings;
    private readonly AppWindow _owner;
    private readonly Action _showWidget;
    private readonly Action _openSettings;
    private readonly Func<Task> _shutdown;
    private readonly WidgetFrame _frame;
    private readonly nint _handle;
    private readonly DispatcherQueueTimer _visibilityTimer;
    private readonly bool _animationsEnabled;
    private Storyboard? _transition;
    private string? _trackIdentity;
    private bool _disposed;
    private bool _shown;
    private bool _verifying;

    public PlayerViewModel Model { get; }

    internal NowPlayingCapsuleWindow(PlayerViewModel model, SettingsViewModel settings, AppWindow owner,
        Action showWidget, Action openSettings, Func<Task> shutdown)
    {
        Model = model;
        _settings = settings;
        _owner = owner;
        _showWidget = showWidget;
        _openSettings = openSettings;
        _shutdown = shutdown;
        _animationsEnabled = AreAnimationsEnabled();
        InitializeComponent();
        Title = "Mediance · Now playing";
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Mediance.ico");
        if (File.Exists(iconPath)) AppWindow.SetIcon(iconPath);
        _frame = new(this, 220, HeightDip, noActivate: true);
        NativeWindowFeatures.ConfigurePopupWindow(WinRT.Interop.WindowNative.GetWindowHandle(this));
        _frame.SetTopmost(true);
        SystemBackdrop = null;
        _handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        Model.PropertyChanged += Model_PropertyChanged;
        _settings.Changed += Settings_Changed;
        _visibilityTimer = DispatcherQueue.CreateTimer();
        _visibilityTimer.Interval = TimeSpan.FromMilliseconds(900);
        _visibilityTimer.IsRepeating = true;
        _visibilityTimer.Tick += VisibilityTimer_Tick;
        _visibilityTimer.Start();
        AppWindow.Closing += AppWindow_Closing;
    }

    internal void Start()
    {
        Activate();
        _frame.Hide();
        RefreshLayoutAndVisibility();
    }

    private void VisibilityTimer_Tick(DispatcherQueueTimer sender, object args) => RefreshLayoutAndVisibility();
    private void Settings_Changed(object? sender, EventArgs e) => RefreshLayoutAndVisibility();
    private void Model_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_disposed || _verifying) return;
        var changedTrack = !string.Equals(_trackIdentity, Model.TrackIdentity, StringComparison.Ordinal);
        _trackIdentity = Model.TrackIdentity;
        RefreshLayoutAndVisibility();
        if (changedTrack && _shown) AnimateTrackChange();
    }

    private void RefreshLayoutAndVisibility()
    {
        if (_disposed) return;
        var ownerCenter = new Mediance.Core.Windowing.PixelPoint(
            _owner.Position.X + _owner.Size.Width / 2, _owner.Position.Y + _owner.Size.Height / 2);
        var monitor = NativeWindowFeatures.MonitorAt(ownerCenter);
        var fullscreen = NativeWindowFeatures.IsForegroundFullscreenAt(new(
            monitor.Bounds.X + monitor.Bounds.Width / 2, monitor.Bounds.Y + monitor.Bounds.Height / 2));
        var shouldShow = _settings.ShowNowPlayingCapsule && Model.HasMedia && !fullscreen;
        if (!shouldShow)
        {
            HideCapsule(fullscreen);
            return;
        }

        var widthDip = Math.Clamp(112 + Math.Max(Model.Title.Length * 6.5, Model.Artist.Length * 4.8), 172, 292);
        if (!ResizeAndPlace(widthDip, monitor))
        {
            HideCapsule(true);
            return;
        }
        ShowCapsule();
    }

    private bool ResizeAndPlace(double widthDip, MonitorWorkArea monitor)
    {
        var center = new PixelPoint(monitor.Bounds.X + monitor.Bounds.Width / 2,
            monitor.Bounds.Y + monitor.Bounds.Height / 2);
        var taskbar = NativeWindowFeatures.TaskbarAt(center,
            NativeWindowFeatures.DpiScale(Microsoft.UI.Win32Interop.GetWindowFromWindowId(_owner.Id)));
        if (taskbar is null) return false;
        var placement = TaskbarCapsuleLayout.Place(monitor.Bounds, monitor.WorkArea,
            taskbar.Bounds, taskbar.NotificationArea, taskbar.DpiScale, widthDip);
        if (placement is null) return false;
        var bounds = placement.Bounds;
        // Move first so WinUI picks up the target monitor's DPI before its measure pass.
        AppWindow.Move(new PointInt32(bounds.X, bounds.Y));
        var width = bounds.Width;
        var height = bounds.Height;
        if (AppWindow.ClientSize.Width != width || AppWindow.ClientSize.Height != height)
        {
            AppWindow.ResizeClient(new SizeInt32(width, height));
            NativeWindowFeatures.SetRoundedRegion(_handle, AppWindow.Size.Width, AppWindow.Size.Height, AppWindow.Size.Height / 2);
        }
        return true;
    }

    internal async Task VerifyPlacementAsync(MonitorWorkArea monitor)
    {
        _verifying = true;
        _visibilityTimer.Stop();
        if (!ResizeAndPlace(240, monitor)) return; // no visible taskbar on this display
        _transition?.Stop();
        RootTransform.ScaleX = RootTransform.ScaleY = 1;
        RootTransform.TranslateX = RootTransform.TranslateY = 0;
        Surface.Opacity = 1;
        _frame.ShowWithoutActivation();
        await Task.Delay(220);
        Surface.UpdateLayout();
        var center = new PixelPoint(monitor.Bounds.X + monitor.Bounds.Width / 2, monitor.Bounds.Y + monitor.Bounds.Height / 2);
        var taskbar = NativeWindowFeatures.TaskbarAt(center,
            NativeWindowFeatures.DpiScale(Microsoft.UI.Win32Interop.GetWindowFromWindowId(_owner.Id)))!;
        var expected = TaskbarCapsuleLayout.Place(monitor.Bounds, monitor.WorkArea, taskbar.Bounds,
            taskbar.NotificationArea, taskbar.DpiScale, 240)!;
        if (AppWindow.Position.X != expected.Bounds.X || AppWindow.Position.Y != expected.Bounds.Y ||
            AppWindow.ClientSize.Width != expected.Bounds.Width || AppWindow.ClientSize.Height != expected.Bounds.Height ||
            !NativeWindowFeatures.IsToolWindow(_handle) || !NativeWindowFeatures.IsNoActivateWindow(_handle))
            throw new InvalidOperationException($"Capsule placement: expected={expected.Bounds}, position={AppWindow.Position.X},{AppWindow.Position.Y}, client={AppWindow.ClientSize.Width},{AppWindow.ClientSize.Height}, tool={NativeWindowFeatures.IsToolWindow(_handle)}, noActivate={NativeWindowFeatures.IsNoActivateWindow(_handle)}.");
        var button = PlayButton.TransformToVisual(Surface).TransformPoint(new(0, 0));
        if (button.X < 0 || button.Y < 0 || button.X + PlayButton.ActualWidth > Surface.ActualWidth + 0.5 ||
            button.Y + PlayButton.ActualHeight > Surface.ActualHeight + 0.5)
            throw new InvalidOperationException("Capsule play button escaped its client bounds.");
    }

    internal Task SavePreviewAsync(string path) => WindowPreview.SaveAsync(Surface, path);

    internal void CheckLivePlacement()
    {
        var ownerCenter = new PixelPoint(_owner.Position.X + _owner.Size.Width / 2,
            _owner.Position.Y + _owner.Size.Height / 2);
        var monitor = NativeWindowFeatures.MonitorAt(ownerCenter);
        var taskbar = NativeWindowFeatures.TaskbarAt(ownerCenter,
            NativeWindowFeatures.DpiScale(Microsoft.UI.Win32Interop.GetWindowFromWindowId(_owner.Id)));
        if (!_shown)
        {
            ProbeLog.Write($"CapsuleLiveCheck: hidden, start={NativeWindowFeatures.IsStartMenuForeground()}, media={Model.HasMedia}, enabled={_settings.ShowNowPlayingCapsule}, full={NativeWindowFeatures.IsForegroundFullscreenAt(ownerCenter)}, bar={taskbar?.Bounds}, work={monitor.WorkArea}");
            return;
        }
        if (taskbar is null) return;
        var expected = TaskbarCapsuleLayout.Place(monitor.Bounds, monitor.WorkArea, taskbar.Bounds,
            taskbar.NotificationArea, taskbar.DpiScale,
            Math.Clamp(112 + Math.Max(Model.Title.Length * 6.5, Model.Artist.Length * 4.8), 172, 292));
        if (expected is null) return;
        var passed = AppWindow.Position.X == expected.Bounds.X && AppWindow.Position.Y == expected.Bounds.Y &&
            AppWindow.ClientSize.Width == expected.Bounds.Width && AppWindow.ClientSize.Height == expected.Bounds.Height;
        ProbeLog.Write($"CapsuleLiveCheck: {(passed ? "PASS" : "FAIL")}, start={NativeWindowFeatures.IsStartMenuForeground()}, monitor={monitor.Bounds.Width}x{monitor.Bounds.Height}, dpi={taskbar.DpiScale}, x={AppWindow.Position.X}, y={AppWindow.Position.Y}, size={AppWindow.ClientSize.Width}x{AppWindow.ClientSize.Height}");
    }

    private void ShowCapsule()
    {
        if (_shown && _frame.IsVisible) return;
        _shown = true;
        _transition?.Stop();
        _frame.ShowWithoutActivation();
        if (!_animationsEnabled)
        {
            Surface.Opacity = 1;
            return;
        }
        Surface.Opacity = 0;
        RootTransform.ScaleX = RootTransform.ScaleY = 0.94;
        RootTransform.TranslateY = 7;
        var storyboard = new Storyboard();
        storyboard.Children.Add(Animation(Surface, "Opacity", 0, 1, 190));
        storyboard.Children.Add(Animation(RootTransform, "ScaleX", 0.94, 1, 320));
        storyboard.Children.Add(Animation(RootTransform, "ScaleY", 0.94, 1, 320));
        storyboard.Children.Add(Animation(RootTransform, "TranslateY", 7, 0, 280));
        _transition = storyboard;
        storyboard.Begin();
    }

    private void HideCapsule(bool immediate)
    {
        if (!_shown && !_frame.IsVisible) return;
        _shown = false;
        _transition?.Stop();
        _transition = null;
        if (immediate || !_animationsEnabled)
        {
            Surface.Opacity = 0;
            _frame.Hide();
            return;
        }
        var storyboard = new Storyboard();
        storyboard.Children.Add(Animation(Surface, "Opacity", Surface.Opacity, 0, 130));
        storyboard.Completed += (_, _) =>
        {
            if (!ReferenceEquals(_transition, storyboard) || _shown) return;
            _frame.Hide();
            storyboard.Stop();
            _transition = null;
        };
        _transition = storyboard;
        storyboard.Begin();
    }

    private void AnimateTrackChange()
    {
        if (!_animationsEnabled) return;
        _transition?.Stop();
        Surface.Opacity = 0.35;
        RootTransform.TranslateX = 10;
        RootTransform.ScaleX = RootTransform.ScaleY = 0.985;
        var storyboard = new Storyboard();
        storyboard.Children.Add(Animation(Surface, "Opacity", 0.35, 1, 260));
        storyboard.Children.Add(Animation(RootTransform, "TranslateX", 10, 0, 380));
        storyboard.Children.Add(Animation(RootTransform, "ScaleX", 0.985, 1, 380));
        storyboard.Children.Add(Animation(RootTransform, "ScaleY", 0.985, 1, 380));
        _transition = storyboard;
        storyboard.Begin();
    }

    private void Surface_PointerEntered(object sender, PointerRoutedEventArgs e) => AnimateHover(1);
    private void Surface_PointerExited(object sender, PointerRoutedEventArgs e) => AnimateHover(1);
    private void AnimateHover(double scale)
    {
        if (!_animationsEnabled) return;
        var storyboard = new Storyboard();
        storyboard.Children.Add(Animation(RootTransform, "ScaleX", RootTransform.ScaleX, scale, 180));
        storyboard.Children.Add(Animation(RootTransform, "ScaleY", RootTransform.ScaleY, scale, 180));
        storyboard.Begin();
    }

    private void Surface_Tapped(object sender, TappedRoutedEventArgs e)
    {
        for (var current = e.OriginalSource as DependencyObject; current is not null;
             current = VisualTreeHelper.GetParent(current))
            if (current is ButtonBase) return;
        _showWidget();
    }
    private async void Play_Click(object sender, RoutedEventArgs e) => await Model.SendAsync(MediaCommand.Toggle);
    private async void Next_Click(object sender, RoutedEventArgs e) => await Model.SendAsync(MediaCommand.Next);
    private void Settings_Click(object sender, RoutedEventArgs e) => _openSettings();
    private async void Exit_Click(object sender, RoutedEventArgs e) => await _shutdown();

    private static DoubleAnimation Animation(DependencyObject target, string property, double from, double to, int milliseconds)
    {
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = new Duration(TimeSpan.FromMilliseconds(milliseconds)),
            EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, property);
        return animation;
    }

    private static bool AreAnimationsEnabled()
    {
        try { return new UISettings().AnimationsEnabled; }
        catch { return true; }
    }

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (!_disposed) args.Cancel = true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _visibilityTimer.Stop();
        _visibilityTimer.Tick -= VisibilityTimer_Tick;
        Model.PropertyChanged -= Model_PropertyChanged;
        _settings.Changed -= Settings_Changed;
        AppWindow.Closing -= AppWindow_Closing;
        _transition?.Stop();
        Close();
    }
}
