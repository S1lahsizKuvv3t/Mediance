using System.ComponentModel;
using Mediance.AcrylicProbe.ViewModels;
using Mediance.AcrylicProbe.Windowing;
using Mediance.Core.Media;
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
        if (_disposed) return;
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
        ResizeAndPlace(widthDip, monitor);
        ShowCapsule();
    }

    private void ResizeAndPlace(double widthDip, MonitorWorkArea monitor)
    {
        var scale = NativeWindowFeatures.DpiScale(_handle);
        var width = Math.Min((int)Math.Ceiling(widthDip * scale), monitor.WorkArea.Width - 16);
        var height = (int)Math.Ceiling(HeightDip * scale);
        if (AppWindow.ClientSize.Width != width || AppWindow.ClientSize.Height != height)
        {
            AppWindow.ResizeClient(new SizeInt32(width, height));
            NativeWindowFeatures.SetRoundedRegion(_handle, AppWindow.Size.Width, AppWindow.Size.Height, AppWindow.Size.Height / 2);
        }

        var gap = (int)Math.Round(4 * scale);
        var center = new Mediance.Core.Windowing.PixelPoint(
            monitor.Bounds.X + monitor.Bounds.Width / 2, monitor.Bounds.Y + monitor.Bounds.Height / 2);
        var taskbar = NativeWindowFeatures.TaskbarAt(center);
        var (x, y) = taskbar is null
            ? AboveWorkArea(monitor.WorkArea, AppWindow.Size.Width, AppWindow.Size.Height, gap)
            : InsideTaskbar(taskbar, AppWindow.Size.Width, AppWindow.Size.Height, gap);
        AppWindow.Move(new PointInt32(x, y));
    }

    private static (int X, int Y) InsideTaskbar(TaskbarPlacement taskbar, int width, int height, int gap)
    {
        var bar = taskbar.Bounds;
        var horizontal = bar.Width >= bar.Height;
        if (horizontal)
        {
            var anchor = taskbar.NotificationArea?.X ?? bar.X + bar.Width - Math.Min(280, bar.Width / 4);
            var x = Math.Clamp(anchor - width - gap, bar.X + gap, bar.X + bar.Width - width - gap);
            var band = taskbar.NotificationArea ?? bar;
            var y = Math.Clamp(band.Y + Math.Max(0, (band.Height - height) / 2),
                bar.Y, bar.Y + bar.Height - height);
            return (x, y);
        }

        var verticalAnchor = taskbar.NotificationArea?.Y ?? bar.Y + bar.Height - Math.Min(220, bar.Height / 4);
        var verticalX = bar.X + Math.Max(0, (bar.Width - width) / 2);
        var verticalY = Math.Clamp(verticalAnchor - height - gap, bar.Y + gap, bar.Y + bar.Height - height - gap);
        return (verticalX, verticalY);
    }

    private static (int X, int Y) AboveWorkArea(Mediance.Core.Windowing.PixelRect work, int width, int height, int gap) =>
        (work.X + work.Width - width - gap, work.Y + work.Height - height - gap);

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

    private void Surface_PointerEntered(object sender, PointerRoutedEventArgs e) => AnimateHover(1.025);
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
