namespace Mediance.Core.Settings;

public enum ThemePreset { Midnight, Prism, ClearGlass }

public sealed record SavedWindowPlacement(int X, int Y);

public sealed record WidgetSettings
{
    private static readonly IReadOnlyDictionary<string, SavedWindowPlacement> EmptyMonitorPlacements =
        new Dictionary<string, SavedWindowPlacement>(StringComparer.OrdinalIgnoreCase);
    public int SchemaVersion { get; init; } = 1;
    public bool AlwaysOnTop { get; init; }
    public bool IsLocked { get; init; }
    public bool SolidBackground { get; init; }
    public double GlassIntensity { get; init; } = 55;
    public bool ShowArtwork { get; init; } = true;
    public bool ShowProgress { get; init; } = true;
    public bool EnableAmbientGlow { get; init; } = true;
    public bool EnableWheelVolume { get; init; } = true;
    public bool CloseToTray { get; init; }
    public bool ShowTitle { get; init; } = true;
    public bool ShowArtist { get; init; } = true;
    public bool ShowSource { get; init; } = true;
    public bool ShowControls { get; init; } = true;
    public bool ShowPrevious { get; init; } = true;
    public bool ShowPlayPause { get; init; } = true;
    public bool ShowNext { get; init; } = true;
    public bool ShowBrand { get; init; } = true;
    public bool ShowCloseButton { get; init; } = true;
    public bool ShowBorder { get; init; } = true;
    public bool ShowPlaybackStatus { get; init; }
    public bool ShowLyricsButton { get; init; } = true;
    public bool LyricsOpen { get; init; }
    public int LyricsLineCount { get; init; } = 3;
    public bool StartWithWindows { get; init; }
    public ThemePreset Theme { get; init; } = ThemePreset.Midnight;
    public bool ShowAudioOutput { get; init; } = true;
    public int? WindowX { get; init; }
    public int? WindowY { get; init; }
    public string? LastMonitorId { get; init; }
    public IReadOnlyDictionary<string, SavedWindowPlacement> MonitorPlacements { get; init; } =
        EmptyMonitorPlacements;
    public double WindowWidth { get; init; } = 520;
    public double ArtworkSize { get; init; } = 84;
    public double TextScale { get; init; } = 100;
    public double ControlSize { get; init; } = 52;
    public double LyricsLeadMilliseconds { get; init; } = 500;
    public HotkeyModifiers ShortcutModifiers { get; init; } = HotkeyGesture.Default.Modifiers;
    public uint ShortcutVirtualKey { get; init; } = HotkeyGesture.Default.VirtualKey;

    public WidgetSettings Normalize()
    {
        var shortcut = new HotkeyGesture(ShortcutModifiers, ShortcutVirtualKey);
        if (!shortcut.IsValid) shortcut = HotkeyGesture.Default;
        return this with
        {
            GlassIntensity = FiniteClamp(GlassIntensity, 10, 95, 55),
            WindowWidth = FiniteClamp(WindowWidth, 420, 720, 520),
            ArtworkSize = FiniteClamp(ArtworkSize, 48, 120, 84),
            TextScale = FiniteClamp(TextScale, 80, 140, 100),
            ControlSize = FiniteClamp(ControlSize, 36, 72, 52),
            LyricsLeadMilliseconds = FiniteClamp(LyricsLeadMilliseconds, -2000, 2000, 500),
            LyricsLineCount = LyricsLineCount == 2 ? 2 : 3,
            Theme = Enum.IsDefined(Theme) ? Theme : ThemePreset.Midnight,
            MonitorPlacements = MonitorPlacements ?? EmptyMonitorPlacements,
            ShortcutModifiers = shortcut.Modifiers,
            ShortcutVirtualKey = shortcut.VirtualKey
        };
    }

    private static double FiniteClamp(double value, double min, double max, double fallback) =>
        double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
}
