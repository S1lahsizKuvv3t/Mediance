using Mediance.Core.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Mediance.AcrylicProbe.Windowing;

internal sealed class WindowAppearance(Window window, Border surface) : IDisposable
{
    private readonly GlassBackdrop _glass = new();
    internal string NativeState => _glass.NativeState;
    internal byte OverlayAlpha => (surface.Background as SolidColorBrush)?.Color.A ?? 0;
    internal bool IsSolid => window.SystemBackdrop is null;

    internal void Apply(WidgetSettings settings)
    {
        _glass.Update(settings.Theme);
        var solid = settings.SolidBackground || !_glass.Supported;
        if (solid) window.SystemBackdrop = null;
        else if (window.SystemBackdrop != _glass) window.SystemBackdrop = _glass;
        // This layer is over a real Desktop Acrylic backdrop, not a simulated blur.
        // Unlike changing just the native tint coefficient, this creates an obvious
        // low-to-high density range while text and icons retain full opacity.
        var baseColor = settings.Theme switch
        {
            ThemePreset.Prism => (R: (byte)20, G: (byte)25, B: (byte)48),
            ThemePreset.ClearGlass => (R: (byte)25, G: (byte)27, B: (byte)30),
            _ => (R: (byte)20, G: (byte)24, B: (byte)30)
        };
        var solidColor = settings.Theme switch
        {
            ThemePreset.Prism => Color.FromArgb(255, 20, 24, 42),
            ThemePreset.ClearGlass => Color.FromArgb(255, 31, 32, 35),
            _ => GlassBackdrop.SolidColor
        };
        var alpha = (byte)Math.Round(settings.GlassIntensity / 100 * 255);
        if (settings.Theme == ThemePreset.ClearGlass) alpha = (byte)Math.Round(alpha * 0.62);
        surface.Background = new SolidColorBrush(solid ? solidColor : Color.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B));
        var border = settings.Theme == ThemePreset.Prism
            ? Color.FromArgb(settings.ShowBorder ? (byte)92 : (byte)0, 99, 173, 255)
            : Color.FromArgb(settings.ShowBorder ? (byte)37 : (byte)0, 255, 255, 255);
        surface.BorderBrush = new SolidColorBrush(border);
    }
    public void Dispose() => window.SystemBackdrop = null;
}
