using Mediance.Core.Lyrics;
using Mediance.Lyrics;
using Mediance.Windows.Media;
using System.Globalization;

await using var media = new WindowsMediaSessionService();
try
{
    await media.StartAsync();
    var selected = media.Snapshot.Selected;
    LyricsQuery query;
    string source;
    if (args.Length >= 2)
    {
        TimeSpan? duration = args.Length >= 4 && double.TryParse(args[3], NumberStyles.Float,
            CultureInfo.InvariantCulture, out var seconds) ? TimeSpan.FromSeconds(seconds) : null;
        query = new(args[0], args[1], args.Length >= 3 ? args[2] : null, duration);
        source = "explicit diagnostic query";
    }
    else
    {
        if (selected is null || string.IsNullOrWhiteSpace(selected.Track.Title) ||
            string.IsNullOrWhiteSpace(selected.Track.Artist))
        {
            Console.WriteLine("No selected song with title and artist metadata.");
            return 2;
        }
        query = new(selected.Track.Title, selected.Track.Artist, selected.Track.Album,
            selected.Timeline.End > TimeSpan.Zero ? selected.Timeline.End : null);
        source = selected.SourceAppId;
    }

    using var http = new HttpClient();
    var service = new LyricsService(new FallbackLyricsProvider(
        new LrcLibLyricsProvider(http),
        new BetterLyricsProvider(http),
        new AmllLyricsProvider(http),
        new AppleMusicLyricsProvider(http),
        new FirstAvailableLyricsProvider(
            new SarkiAnaliziLyricsProvider(http),
            new SozMuzikLyricsProvider(http),
            new GeniusLyricsProvider(http),
            new BbsLyricsProvider(http))));
    var document = await service.FindAsync(query);
    Console.WriteLine($"Source: {source}");
    Console.WriteLine($"Track: {query.Title} — {query.Artist}");
    Console.WriteLine($"Result: {document.Kind}; timed lines={document.Lines.Count}; plain={document.PlainText is not null}");
    if (document.Lines.Count > 0)
    {
        var position = selected?.Timeline.Position ?? TimeSpan.FromSeconds(30);
        var active = LyricsTimeline.ActiveLineIndex(document.Lines, position);
        Console.WriteLine($"Range: {document.Lines[0].Start:c}–{document.Lines[^1].Start:c}; active index={active}");
    }
    Console.WriteLine("Lyrics content was kept in memory and was not written to disk or logs.");
    return document.Kind == LyricsKind.Unavailable ? 3 : 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Lyrics probe failed: {ex.GetType().Name} (0x{ex.HResult:X8}): {ex.Message}");
    return 1;
}
