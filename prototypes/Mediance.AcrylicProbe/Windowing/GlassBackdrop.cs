using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Mediance.Core.Settings;
using Windows.UI;

namespace Mediance.AcrylicProbe.Windowing;

// One controller per window; no shared backdrop instances.
internal sealed class GlassBackdrop : SystemBackdrop
{
    private DesktopAcrylicController? _controller;
    private SystemBackdropConfiguration? _configuration;
    internal static Color SolidColor => Color.FromArgb(255, 27, 30, 35);
    internal bool Supported => DesktopAcrylicController.IsSupported();
    internal string NativeState => _controller?.State.ToString() ?? "Disconnected";
    private ThemePreset _theme;

    internal void Update(ThemePreset theme)
    {
        _theme = theme;
        ApplyTheme();
    }

    private void ApplyTheme()
    {
        if (_controller is null) return;
        var (tint, tintOpacity, luminosity, fallback) = _theme switch
        {
            ThemePreset.Prism => (Color.FromArgb(255, 22, 29, 54), 0.10f, 0.13f, Color.FromArgb(255, 20, 24, 42)),
            ThemePreset.ClearGlass => (Color.FromArgb(255, 30, 32, 36), 0.01f, 0.04f, Color.FromArgb(255, 31, 32, 35)),
            _ => (Color.FromArgb(255, 20, 24, 30), 0.03f, 0.08f, SolidColor)
        };
        _controller.TintColor = tint;
        _controller.TintOpacity = tintOpacity;
        _controller.LuminosityOpacity = luminosity;
        _controller.FallbackColor = fallback;
    }

    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop target, XamlRoot xamlRoot)
    {
        base.OnTargetConnected(target, xamlRoot);
        if (!Supported) return;
        DispatcherQueue.EnsureSystemDispatcherQueue();
        // The floating widget keeps its material when the user works in another
        // window. The controller still honors system transparency/power/contrast policy.
        _configuration = new SystemBackdropConfiguration { IsInputActive = true, Theme = SystemBackdropTheme.Dark };
        _controller = new DesktopAcrylicController
        {
            Kind = DesktopAcrylicKind.Thin,
            TintColor = Color.FromArgb(255, 20, 24, 30),
            // Keep the underlying real blur lightly tinted. The visible density
            // is controlled by the XAML tint layer, independently of foreground.
            TintOpacity = 0.03f,
            LuminosityOpacity = 0.08f,
            FallbackColor = SolidColor
        };
        _controller.SetSystemBackdropConfiguration(_configuration);
        _controller.AddSystemBackdropTarget(target);
        ApplyTheme();
        ProbeLog.Write("AcrylicConnected");
    }

    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop target)
    {
        if (_controller is not null)
        {
            _controller.RemoveSystemBackdropTarget(target);
            _controller.Dispose();
            _controller = null;
        }
        _configuration = null;
        base.OnTargetDisconnected(target);
    }
}
