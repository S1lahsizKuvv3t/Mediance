using Mediance.Core.Media;
using Xunit;

namespace Mediance.Core.Tests;

public sealed class MediaTests
{
    private static MediaSessionInfo Session(string id, PlaybackStatus status, string app = "browser",
        MediaKind kind = MediaKind.Unknown) =>
        new(id, app, new("Track", "Artist", "Album"), status, 1,
            Timeline(10), new(true, true, true, true, true, true), false, Kind: kind);

    private static PlaybackTimeline Timeline(double position, double end = 200, double start = 0) =>
        new(TimeSpan.FromSeconds(position), TimeSpan.FromSeconds(start), TimeSpan.FromSeconds(end),
            TimeSpan.FromSeconds(start), TimeSpan.FromSeconds(end), DateTimeOffset.UnixEpoch);

    [Fact]
    public void DifferentCurrentObjectResolvesByUniqueSourceEvenWhenAnotherSourceIsPlaying()
    {
        var sessions = new[] { Session("1", PlaybackStatus.Playing, "Chrome"), Session("2", PlaybackStatus.Paused, "Spotify") };
        var current = SessionSelection.ResolveCurrentSessionId(sessions, null, "Spotify");
        Assert.Equal("2", current);
        Assert.Equal("2", SessionSelection.Choose(sessions, current, null).SelectedId);
    }

    [Fact]
    public void SourceFallbackNeverClaimsAnAmbiguousBrowserSessionIsCurrent()
    {
        var sessions = new[] { Session("tab1", PlaybackStatus.Playing), Session("tab2", PlaybackStatus.Paused) };
        Assert.Null(SessionSelection.ResolveCurrentSessionId(sessions, null, "browser"));
    }

    [Fact]
    public void ExactCurrentMatchDistinguishesSessionsWithinSameApplication()
    {
        var sessions = new[] { Session("tab1", PlaybackStatus.Playing), Session("tab2", PlaybackStatus.Paused) };
        Assert.Equal("tab2", SessionSelection.ResolveCurrentSessionId(sessions, "tab2", "browser"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-present")]
    public void MissingCurrentSourceDoesNotInventAMatch(string? source)
    {
        Assert.Null(SessionSelection.ResolveCurrentSessionId([Session("1", PlaybackStatus.Playing)], null, source));
    }

    [Fact]
    public void StaleExactMatchCanResolveToTheOnlyRemainingSourceSession()
    {
        Assert.Equal("new", SessionSelection.ResolveCurrentSessionId(
            [Session("new", PlaybackStatus.Playing)], "old", "browser"));
    }

    [Fact]
    public void ResolvedWindowsCurrentStillDoesNotOverridePinnedSession()
    {
        var sessions = new[] { Session("1", PlaybackStatus.Playing, "Chrome"), Session("2", PlaybackStatus.Paused, "Spotify") };
        var current = SessionSelection.ResolveCurrentSessionId(sessions, null, "Chrome");
        Assert.Equal(("2", "2"), SessionSelection.Choose(sessions, current, "2"));
    }

    [Fact]
    public void PinRemainsWhenAnotherApplicationBecomesCurrent()
    {
        var result = SessionSelection.Choose(
            [Session("spotify", PlaybackStatus.Paused), Session("chrome", PlaybackStatus.Playing)], "chrome", "spotify");
        Assert.Equal(("spotify", "spotify"), result);
    }

    [Fact]
    public void DisappearingPinReturnsToFollowMode()
    {
        var result = SessionSelection.Choose([Session("chrome", PlaybackStatus.Playing)], "chrome", "spotify");
        Assert.Equal("chrome", result.SelectedId);
        Assert.Null(result.PinnedId);
    }

    [Fact]
    public void SeparateSessionsFromSameBrowserCanBePinnedIndependently()
    {
        var result = SessionSelection.Choose(
            [Session("tab1", PlaybackStatus.Paused), Session("tab2", PlaybackStatus.Playing)], "tab2", "tab1");
        Assert.Equal("tab1", result.SelectedId);
    }

    [Fact]
    public void WindowsCurrentSessionHasPriorityInFollowMode()
    {
        var result = SessionSelection.Choose(
            [Session("a", PlaybackStatus.Playing), Session("b", PlaybackStatus.Paused)], "b", null);
        Assert.Equal("b", result.SelectedId);
    }

    [Fact]
    public void SpotifyStaysSelectedWhenAYouTubeVideoBecomesCurrent()
    {
        var result = SessionSelection.Choose(
            [Session("spotify", PlaybackStatus.Paused, "Spotify.exe", MediaKind.Music),
             Session("youtube", PlaybackStatus.Playing, "opera.exe", MediaKind.Video)],
            "youtube", null);
        Assert.Equal("spotify", result.SelectedId);
    }

    [Fact]
    public void BrowserMusicIsPreferredOverNormalYouTubeVideoWhenSpotifyIsAbsent()
    {
        var result = SessionSelection.Choose(
            [Session("music", PlaybackStatus.Paused, "chrome.exe", MediaKind.Music),
             Session("video", PlaybackStatus.Playing, "opera.exe", MediaKind.Video)],
            "video", null);
        Assert.Equal("music", result.SelectedId);
    }

    [Fact]
    public void NormalYouTubeVideoStillFollowsWindowsWhenPreferredSourcesAreAbsent()
    {
        var result = SessionSelection.Choose(
            [Session("video", PlaybackStatus.Playing, "opera.exe", MediaKind.Video),
             Session("other", PlaybackStatus.Paused, "vlc.exe")], "video", null);
        Assert.Equal("video", result.SelectedId);
    }

    [Fact]
    public void ExplicitPinOverridesSpotifyPreference()
    {
        var result = SessionSelection.Choose(
            [Session("spotify", PlaybackStatus.Playing, "Spotify.exe", MediaKind.Music),
             Session("video", PlaybackStatus.Paused, "opera.exe", MediaKind.Video)],
            "spotify", "video");
        Assert.Equal(("video", "video"), result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("stale-id")]
    public void MissingCurrentSessionFallsBackToPlayingSource(string? currentId)
    {
        var result = SessionSelection.Choose(
            [Session("a", PlaybackStatus.Paused), Session("b", PlaybackStatus.Playing)], currentId, null);
        Assert.Equal("b", result.SelectedId);
    }

    [Fact]
    public void EmptyCollectionClearsSelectionAndPin()
    {
        var result = SessionSelection.Choose([], "old", "old");
        Assert.Null(result.SelectedId);
        Assert.Null(result.PinnedId);
    }

    [Fact]
    public void TimelineInterpolatesUsingPlaybackRate()
    {
        var time = new TestTimeProvider();
        var clock = new TimelineClock(time);
        clock.Reconcile(Timeline(10), true, 1.5);
        time.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(TimeSpan.FromSeconds(13), clock.Position);
    }

    [Fact]
    public void PausedTimelineDoesNotAdvance()
    {
        var time = new TestTimeProvider();
        var clock = new TimelineClock(time);
        clock.Reconcile(Timeline(10), false, 1);
        time.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(TimeSpan.FromSeconds(10), clock.Position);
    }

    [Fact]
    public void PlayingTimelineAccountsForSnapshotAgeAtReconciliation()
    {
        var time = new TestTimeProvider { Utc = DateTimeOffset.UnixEpoch.AddSeconds(100) };
        var clock = new TimelineClock(time);
        var timeline = Timeline(10) with { LastUpdated = time.Utc.AddSeconds(-1.5) };
        clock.Reconcile(timeline, true, 1);
        Assert.Equal(TimeSpan.FromSeconds(11.5), clock.Position);
    }

    [Fact]
    public void TimelineClampsAtEnd()
    {
        var time = new TestTimeProvider();
        var clock = new TimelineClock(time);
        clock.Reconcile(Timeline(199), true, 1);
        time.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal(TimeSpan.FromSeconds(200), clock.Position);
    }

    [Fact]
    public void SeekBackwardsReplacesOldInterpolationAnchor()
    {
        var time = new TestTimeProvider();
        var clock = new TimelineClock(time);
        clock.Reconcile(Timeline(100), true, 1);
        time.Advance(TimeSpan.FromSeconds(10));
        clock.Reconcile(Timeline(20), true, 1);
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(TimeSpan.FromSeconds(21), clock.Position);
    }

    [Theory]
    [InlineData(-10, 0, 100, 0)]
    [InlineData(200, 0, 100, 100)]
    [InlineData(0, 10, 100, 10)]
    [InlineData(20, 10, 5, 10)]
    public void InvalidTimelineValuesRemainInsideBounds(double position, double start, double end, double expected)
    {
        var clock = new TimelineClock(new TestTimeProvider());
        clock.Reconcile(Timeline(position, end, start), false, 1);
        Assert.Equal(TimeSpan.FromSeconds(expected), clock.Position);
    }

    [Fact]
    public void UnsupportedCommandsAreExplicit()
    {
        var capabilities = new MediaCapabilities(true, true, false, false, false, false);
        Assert.False(capabilities.Supports(MediaCommand.Seek));
        Assert.False(capabilities.Supports(MediaCommand.Next));
        Assert.True(capabilities.Supports(MediaCommand.Pause));
    }

    [Fact]
    public void WallClockChangesDoNotMovePlaybackPosition()
    {
        var time = new TestTimeProvider();
        var clock = new TimelineClock(time);
        clock.Reconcile(Timeline(10), true, 1);
        time.Utc = DateTimeOffset.UtcNow.AddDays(-1);
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(TimeSpan.FromSeconds(11), clock.Position);
    }

    private sealed class TestTimeProvider : TimeProvider
    {
        private long _ticks;
        public DateTimeOffset Utc { get; set; } = DateTimeOffset.UnixEpoch;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _ticks;
        public override DateTimeOffset GetUtcNow() => Utc;
        public void Advance(TimeSpan elapsed) => _ticks += elapsed.Ticks;
    }
}
