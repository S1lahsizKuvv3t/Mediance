using System.Globalization;

namespace Mediance.Core.Lyrics;

public static class LrcParser
{
    public static IReadOnlyList<LyricLine> Parse(string? lrc)
    {
        if (string.IsNullOrWhiteSpace(lrc)) return [];
        var offset = ReadOffset(lrc);
        var parsed = new List<(LyricLine Line, int Order)>();
        var order = 0;
        foreach (var rawLine in lrc.Replace("\r", "", StringComparison.Ordinal).Split('\n'))
        {
            var cursor = 0;
            var times = new List<TimeSpan>();
            while (cursor < rawLine.Length && rawLine[cursor] == '[')
            {
                var close = rawLine.IndexOf(']', cursor + 1);
                if (close < 0) break;
                if (!TryTimestamp(rawLine.AsSpan(cursor + 1, close - cursor - 1), out var time)) break;
                times.Add(time);
                cursor = close + 1;
            }
            if (times.Count == 0) continue;
            var text = rawLine[cursor..].Trim();
            foreach (var time in times)
            {
                var adjusted = time + offset;
                if (adjusted < TimeSpan.Zero) adjusted = TimeSpan.Zero;
                parsed.Add((new(adjusted, text), order++));
            }
        }
        return parsed.OrderBy(x => x.Line.Start).ThenBy(x => x.Order).Select(x => x.Line).ToArray();
    }

    private static TimeSpan ReadOffset(string lrc)
    {
        foreach (var raw in lrc.Replace("\r", "", StringComparison.Ordinal).Split('\n'))
        {
            var line = raw.Trim();
            if (!line.StartsWith("[offset:", StringComparison.OrdinalIgnoreCase) || !line.EndsWith(']')) continue;
            if (int.TryParse(line.AsSpan(8, line.Length - 9), NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture, out var milliseconds))
                return TimeSpan.FromMilliseconds(milliseconds);
        }
        return TimeSpan.Zero;
    }

    private static bool TryTimestamp(ReadOnlySpan<char> value, out TimeSpan result)
    {
        result = default;
        var separator = value.IndexOf(':');
        if (separator <= 0) return false;
        var relativeFraction = value[(separator + 1)..].IndexOfAny('.', ':');
        ReadOnlySpan<char> secondsPart;
        ReadOnlySpan<char> fractionPart = [];
        if (relativeFraction >= 0)
        {
            var fractionSeparator = relativeFraction + separator + 1;
            secondsPart = value[(separator + 1)..fractionSeparator];
            fractionPart = value[(fractionSeparator + 1)..];
        }
        else secondsPart = value[(separator + 1)..];

        if (!int.TryParse(value[..separator], NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) ||
            !int.TryParse(secondsPart, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) ||
            minutes < 0 || seconds is < 0 or >= 60 || fractionPart.Length > 3) return false;
        var milliseconds = 0;
        if (fractionPart.Length > 0)
        {
            if (!int.TryParse(fractionPart, NumberStyles.None, CultureInfo.InvariantCulture, out var fraction)) return false;
            milliseconds = fractionPart.Length switch { 1 => fraction * 100, 2 => fraction * 10, _ => fraction };
        }
        result = TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds) + TimeSpan.FromMilliseconds(milliseconds);
        return true;
    }
}
