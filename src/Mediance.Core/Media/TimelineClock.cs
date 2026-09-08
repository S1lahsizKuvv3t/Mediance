namespace Mediance.Core.Media;

/// <summary>Visual interpolation only. Does not access Windows or wall time per frame.</summary>
public sealed class TimelineClock(TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private long _anchor;
    private TimeSpan _position;
    private TimeSpan _start;
    private TimeSpan _end;
    private bool _playing;
    private double _rate = 1;

    public void Reconcile(PlaybackTimeline timeline, bool playing, double rate)
    {
        _start = timeline.Start;
        _end = timeline.End < _start ? _start : timeline.End;
        _rate = double.IsFinite(rate) && rate >= 0 ? rate : 1;
        var position = timeline.Position;
        if (playing && timeline.LastUpdated > DateTimeOffset.UnixEpoch)
        {
            var snapshotAge = _time.GetUtcNow() - timeline.LastUpdated;
            if (snapshotAge > TimeSpan.Zero && snapshotAge < TimeSpan.FromHours(24))
                position += TimeSpan.FromSeconds(snapshotAge.TotalSeconds * _rate);
        }
        _position = Clamp(position);
        _playing = playing;
        _anchor = _time.GetTimestamp();
    }

    public TimeSpan Position
    {
        get
        {
            var elapsed = _playing ? _time.GetElapsedTime(_anchor).TotalSeconds * _rate : 0;
            var seconds = Math.Clamp(_position.TotalSeconds + elapsed, _start.TotalSeconds, _end.TotalSeconds);
            return TimeSpan.FromSeconds(seconds);
        }
    }

    private TimeSpan Clamp(TimeSpan value) => value < _start ? _start : value > _end ? _end : value;
}
