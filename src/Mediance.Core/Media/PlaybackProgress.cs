namespace Mediance.Core.Media;

public static class PlaybackProgress
{
    public static double Fraction(TimeSpan position, TimeSpan start, TimeSpan end)
    {
        var duration = (end - start).TotalSeconds;
        if (!double.IsFinite(duration) || duration <= 0) return 0;
        return Math.Clamp((position - start).TotalSeconds / duration, 0, 1);
    }

    public static TimeSpan PositionAt(double fraction, TimeSpan start, TimeSpan end)
    {
        if (!double.IsFinite(fraction)) fraction = 0;
        var duration = end - start;
        if (duration <= TimeSpan.Zero) return start;
        return start + TimeSpan.FromTicks((long)Math.Round(duration.Ticks * Math.Clamp(fraction, 0, 1)));
    }
}
