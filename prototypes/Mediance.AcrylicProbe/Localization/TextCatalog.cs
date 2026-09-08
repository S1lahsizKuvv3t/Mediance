using System.Globalization;
using System.Resources;

namespace Mediance.AcrylicProbe.Localization;

public static class TextCatalog
{
    private static readonly ResourceManager Resources = new("Mediance.AcrylicProbe.Resources.Strings", typeof(TextCatalog).Assembly);
    internal static string Get(string key) => Resources.GetString(key, CultureInfo.CurrentUICulture) ?? key;
    public static string AppName => Get("AppName");
    public static string Close => Get("Close");
    public static string DragHint => Get("DragHint");
    public static string Topmost => Get("Topmost");
    public static string Solid => Get("Solid");
    public static string GlassStrength => Get("GlassStrength");
    public static string Previous => Get("Previous");
    public static string Next => Get("Next");
    public static string Play => Get("Play");
    public static string Pause => Get("Pause");
    public static string Prototype => Get("Prototype");
    public static string GlassHint => Get("GlassHint");
    public static string Settings => Get("Settings");
    public static string Appearance => Get("Appearance");
    public static string Elements => Get("Elements");
    public static string Sizes => Get("Sizes");
    public static string Reset => Get("Reset");
    public static string AutoSave => Get("AutoSave");
    public static string WindowWidth => Get("WindowWidth");
    public static string ArtworkSize => Get("ArtworkSize");
    public static string TextSize => Get("TextSize");
    public static string ControlSize => Get("ControlSize");
    public static string DensityHint => Get("DensityHint");
    public static string Audio => Get("Audio");
    public static string AudioOutput => Get("AudioOutput");
    public static string LyricsTiming => Get("LyricsTiming");
    public static string GlobalShortcut => Get("GlobalShortcut");
    public static string SelectShortcut => Get("SelectShortcut");
    public static string Refresh => Get("Refresh");
    public static string PlaybackProgress => Get("PlaybackProgress");
    public static string Theme => Get("Theme");
    public static string StartWithWindows => Get("StartWithWindows");
    public static string LyricsLayout => Get("LyricsLayout");
    public static string LyricsManualRestart => Get("LyricsManualRestart");
    public static string LyricsManualCancel => Get("LyricsManualCancel");
    public static string LyricsManualRedo => Get("LyricsManualRedo");
}
