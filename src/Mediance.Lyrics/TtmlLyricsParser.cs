using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Mediance.Core.Lyrics;

namespace Mediance.Lyrics;

public static partial class TtmlLyricsParser
{
    public static IReadOnlyList<LyricLine> Parse(string? ttml)
    {
        if (string.IsNullOrWhiteSpace(ttml)) return [];
        try
        {
            var document = XDocument.Parse(ttml, LoadOptions.PreserveWhitespace);
            return document.Descendants()
                .Where(element => element.Name.LocalName == "p")
                .Select(element =>
                {
                    var begin = element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "begin")?.Value;
                    var text = WhitespaceRegex().Replace(element.Value, " ").Trim();
                    return TryParseTime(begin, out var start) && text.Length > 0 ? new LyricLine(start, text) : null;
                })
                .Where(line => line is not null)
                .Select(line => line!)
                .OrderBy(line => line.Start)
                .DistinctBy(line => (line.Start, line.Text))
                .ToArray();
        }
        catch (Exception exception) when (exception is System.Xml.XmlException or InvalidOperationException)
        {
            return [];
        }
    }

    private static bool TryParseTime(string? value, out TimeSpan result)
    {
        result = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(value)) return false;
        value = value.Trim();
        if (value.EndsWith("ms", StringComparison.OrdinalIgnoreCase) &&
            double.TryParse(value[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var milliseconds))
        {
            result = TimeSpan.FromMilliseconds(milliseconds);
            return result >= TimeSpan.Zero;
        }
        if (value.EndsWith('s') &&
            double.TryParse(value[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
        {
            result = TimeSpan.FromSeconds(seconds);
            return result >= TimeSpan.Zero;
        }
        var parts = value.Split(':');
        if (parts.Length is < 2 or > 3) return false;
        if (!double.TryParse(parts[^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var finalSeconds) ||
            !int.TryParse(parts[^2], NumberStyles.None, CultureInfo.InvariantCulture, out var minutes)) return false;
        var hours = 0;
        if (parts.Length == 3 && !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out hours))
            return false;
        result = TimeSpan.FromHours(hours) + TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(finalSeconds);
        return result >= TimeSpan.Zero;
    }

    [GeneratedRegex("\\s+")]
    private static partial Regex WhitespaceRegex();
}
