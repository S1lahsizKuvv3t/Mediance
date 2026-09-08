namespace Mediance.Core.Lyrics;

public static class LyricsTimeline
{
    public static int ActiveLineIndex(IReadOnlyList<LyricLine> lines, TimeSpan position)
    {
        var low = 0;
        var high = lines.Count - 1;
        var result = -1;
        while (low <= high)
        {
            var middle = low + (high - low) / 2;
            if (lines[middle].Start <= position) { result = middle; low = middle + 1; }
            else high = middle - 1;
        }
        return result;
    }

    public static int BrowseLineIndex(int displayedIndex, int activeIndex, int lineCount, int wheelDelta)
    {
        if (lineCount <= 0) return -1;
        var origin = displayedIndex >= 0 ? displayedIndex : Math.Max(0, activeIndex);
        var step = wheelDelta > 0 ? -1 : wheelDelta < 0 ? 1 : 0;
        return Math.Clamp(origin + step, 0, lineCount - 1);
    }
}
