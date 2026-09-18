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
    public double GlassIntensity { get; init; } = 55;
    public double AlbumBlur { get; init; }
    public double AlbumZoom { get; init; } = 106;
    public double AlbumDarkness { get; init; } = 54;

    public static ThemeProfile FromSettings(WidgetSettings settings) => new()
    {
        Theme = settings.Theme,
        GlassIntensity = settings.GlassIntensity,
        AlbumBlur = settings.AlbumBlur,
        AlbumZoom = settings.AlbumZoom,
        AlbumDarkness = settings.AlbumDarkness
    };

    public WidgetSettings ApplyTo(WidgetSettings settings) => (settings with
    {
        Theme = Theme,
        GlassIntensity = GlassIntensity,
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
