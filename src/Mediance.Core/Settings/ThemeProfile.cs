using System.Text.Json;

namespace Mediance.Core.Settings;

public sealed record ThemeProfile
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public int SchemaVersion { get; init; } = 1;
    public ThemePreset Theme { get; init; } = ThemePreset.Midnight;
    public bool SolidBackground { get; init; }
    public double GlassIntensity { get; init; } = 55;
    public bool EnableAmbientGlow { get; init; } = true;
    public bool ShowBorder { get; init; } = true;
    public double AlbumBlur { get; init; }
    public double AlbumZoom { get; init; } = 106;
    public double AlbumDarkness { get; init; } = 54;

    public static ThemeProfile FromSettings(WidgetSettings settings) => new()
    {
        Theme = settings.Theme,
        SolidBackground = settings.SolidBackground,
        GlassIntensity = settings.GlassIntensity,
        EnableAmbientGlow = settings.EnableAmbientGlow,
        ShowBorder = settings.ShowBorder,
        AlbumBlur = settings.AlbumBlur,
        AlbumZoom = settings.AlbumZoom,
        AlbumDarkness = settings.AlbumDarkness
    };

    public WidgetSettings ApplyTo(WidgetSettings settings) => (settings with
    {
        Theme = Theme,
        SolidBackground = SolidBackground,
        GlassIntensity = GlassIntensity,
        EnableAmbientGlow = EnableAmbientGlow,
        ShowBorder = ShowBorder,
        AlbumBlur = AlbumBlur,
        AlbumZoom = AlbumZoom,
        AlbumDarkness = AlbumDarkness
    }).Normalize();

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    public static ThemeProfile Parse(string json)
    {
        var profile = JsonSerializer.Deserialize<ThemeProfile>(json, Options)
            ?? throw new JsonException("Theme profile must be an object.");
        if (profile.SchemaVersion != 1)
            throw new UnsupportedSettingsVersionException(profile.SchemaVersion);
        var normalized = profile.ApplyTo(new WidgetSettings());
        return FromSettings(normalized);
    }
}
