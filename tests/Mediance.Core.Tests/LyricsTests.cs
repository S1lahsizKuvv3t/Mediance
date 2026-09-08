using System.Net;
using System.Text;
using Mediance.Core.Lyrics;
using Mediance.Lyrics;
using Xunit;

namespace Mediance.Core.Tests;

public sealed class LyricsTests
{
    [Fact]
    public void PlainLyricsCleanupRemovesSectionLabelsButKeepsVocalAdlibs()
    {
        var cleaned = PlainLyricsTimeline.Clean(
            "[Giriş]\nFirst line\n[Nakarat: KAVAK]\nSecond line\n(BAKAN made it)\n[Çıkış]");

        Assert.Equal("First line\nSecond line\n(BAKAN made it)", cleaned);
    }

    [Fact]
    public async Task LyricsServiceNeverFabricatesTimingForPlainLyrics()
    {
        var plain = string.Join('\n', Enumerable.Range(1, 12).Select(index => $"Synthetic line {index}"));
        var service = new LyricsService(new FixedProvider(new(LyricsKind.Plain, [], plain)));

        var result = await service.FindAsync(new("Synthetic", "Artist", null, TimeSpan.FromSeconds(90)));

        Assert.Equal(LyricsKind.Plain, result.Kind);
        Assert.Empty(result.Lines);
    }

    [Fact]
    public async Task LyricsServiceKeepsPlainLyricsWhenDurationIsMissing()
    {
        var service = new LyricsService(new FixedProvider(new(LyricsKind.Plain, [], "First\nSecond")));

        var result = await service.FindAsync(new("Synthetic", "Artist", null, null));

        Assert.Equal(LyricsKind.Plain, result.Kind);
    }

    [Fact]
    public async Task ManualTimingPersistsWithoutWritingTrackOrLyricText()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Mediance-lyrics-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "lyrics-timing.json");
        var query = new LyricsQuery("Private Track", "Private Artist", "Private Album", TimeSpan.FromSeconds(90));
        const string plain = "Secret first line\nSecret second line";
        try
        {
            var writer = new LyricsService(new FixedProvider(new(LyricsKind.Plain, [], plain)),
                new LocalLyricsTimingStore(path));
            var saved = await writer.SaveTimingAsync(query, plain,
                [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(8)]);
            Assert.Equal(LyricsKind.Synced, saved.Kind);
            Assert.True(saved.IsUserTimed);

            var reader = new LyricsService(new FixedProvider(new(LyricsKind.Plain, [], plain)),
                new LocalLyricsTimingStore(path));
            var loaded = await reader.FindAsync(query);
            Assert.Equal(LyricsKind.Synced, loaded.Kind);
            Assert.True(loaded.IsUserTimed);
            Assert.Equal([2, 8], loaded.Lines.Select(line => line.Start.TotalSeconds));

            var json = await File.ReadAllTextAsync(path);
            Assert.DoesNotContain("Private Track", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Private Artist", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Secret first line", json, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task SourceAuthoredTimingKeepsPriorityOverSavedManualTiming()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Mediance-lyrics-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "lyrics-timing.json");
        var query = new LyricsQuery("Track", "Artist", null, TimeSpan.FromSeconds(60));
        try
        {
            var store = new LocalLyricsTimingStore(path);
            await store.SaveAsync(query, "First\nSecond",
                [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(8)]);
            var source = new LyricsDocument(LyricsKind.Synced,
                [new(TimeSpan.FromSeconds(4), "Source first"), new(TimeSpan.FromSeconds(9), "Source second")]);

            var result = await new LyricsService(new FixedProvider(source), store).FindAsync(query);

            Assert.Equal("Source first", result.Lines[0].Text);
            Assert.Equal(4, result.Lines[0].Start.TotalSeconds);
            Assert.False(result.IsUserTimed);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task ManualTimingRecoversTheLastCompleteBackup()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Mediance-lyrics-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "lyrics-timing.json");
        var query = new LyricsQuery("Private Track", "Private Artist", null, TimeSpan.FromSeconds(60));
        const string plain = "Secret first line\nSecret second line";
        try
        {
            var store = new LocalLyricsTimingStore(path);
            await store.SaveAsync(query, plain, [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(8)]);
            await store.SaveAsync(query, plain, [TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(9)]);
            await File.WriteAllTextAsync(path, "{interrupted");

            var recovered = await new LocalLyricsTimingStore(path).LoadAsync(query, plain);

            Assert.NotNull(recovered);
            Assert.Equal([2, 8], recovered.Select(value => value.TotalSeconds));
            Assert.Equal("{interrupted", await File.ReadAllTextAsync(path + ".invalid"));
            Assert.DoesNotContain("Private Track", await File.ReadAllTextAsync(path), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void ParserSortsMultipleTimestampsAndIgnoresMetadata()
    {
        var lines = LrcParser.Parse("[ar:Example]\n[00:05.20][00:01.2] Echo\n[00:03] Middle");
        Assert.Equal([1.2, 3, 5.2], lines.Select(x => x.Start.TotalSeconds));
        Assert.Equal(["Echo", "Middle", "Echo"], lines.Select(x => x.Text));
    }

    [Fact]
    public void ParserAppliesSignedOffsetAndClampsBeforeZero()
    {
        var lines = LrcParser.Parse("[offset:-500]\n[00:00.20] First\n[00:01.00] Second");
        Assert.Equal(TimeSpan.Zero, lines[0].Start);
        Assert.Equal(TimeSpan.FromMilliseconds(500), lines[1].Start);
    }

    [Fact]
    public void TtmlParserReadsClockAndMetricTimesWithoutLeakingMarkup()
    {
        var lines = TtmlLyricsParser.Parse("""
            <tt xmlns="http://www.w3.org/ns/ttml"><body><div>
              <p begin="00:00:01.250"><span begin="1.25s">Synthetic </span><span>first</span></p>
              <p begin="2750ms">Synthetic second</p>
            </div></body></tt>
            """);

        Assert.Equal([1.25, 2.75], lines.Select(line => line.Start.TotalSeconds));
        Assert.Equal(["Synthetic first", "Synthetic second"], lines.Select(line => line.Text));
    }

    [Theory]
    [InlineData(0.9, -1)]
    [InlineData(1, 0)]
    [InlineData(9.9, 0)]
    [InlineData(10, 1)]
    [InlineData(50, 2)]
    public void TimelineFindsActiveLineAfterNormalPlaybackOrSeek(double seconds, int expected)
    {
        LyricLine[] lines = [new(TimeSpan.FromSeconds(1), "A"), new(TimeSpan.FromSeconds(10), "B"),
            new(TimeSpan.FromSeconds(20), "C")];
        Assert.Equal(expected, LyricsTimeline.ActiveLineIndex(lines, TimeSpan.FromSeconds(seconds)));
    }

    [Fact]
    public void ManualLyricsBrowsingMovesOneLineAndClampsAtTheEnds()
    {
        Assert.Equal(3, LyricsTimeline.BrowseLineIndex(2, 8, 10, -120));
        Assert.Equal(1, LyricsTimeline.BrowseLineIndex(2, 8, 10, 120));
        Assert.Equal(0, LyricsTimeline.BrowseLineIndex(0, 8, 10, 120));
        Assert.Equal(9, LyricsTimeline.BrowseLineIndex(9, 8, 10, -120));
        Assert.Equal(8, LyricsTimeline.BrowseLineIndex(-1, 8, 10, 0));
    }

    [Fact]
    public async Task ProviderEncodesExactMetadataAndReturnsSyncedLyrics()
    {
        var handler = new StubHandler(HttpStatusCode.OK,
            """{"instrumental":false,"plainLyrics":"A\nB","syncedLyrics":"[00:01.00] A\n[00:02.50] B"}""");
        var provider = new LrcLibLyricsProvider(new HttpClient(handler));
        var result = await provider.FindAsync(new("A & B", "Artist Name", "Album/One", TimeSpan.FromSeconds(122.4)));
        Assert.Equal(LyricsKind.Synced, result.Kind);
        Assert.Equal(2, result.Lines.Count);
        Assert.Contains("track_name=A%20%26%20B", handler.RequestUri!.Query);
        Assert.Contains("album_name=Album%2FOne", handler.RequestUri.Query);
        Assert.Contains("duration=122", handler.RequestUri.Query);
        Assert.Contains("Mediance", handler.UserAgent);
    }

    [Fact]
    public async Task ProviderOmitsMissingAlbumAndHandlesNotFound()
    {
        var handler = new SequenceHandler(
            (HttpStatusCode.NotFound, "{}"),
            (HttpStatusCode.OK, "[]"));
        var result = await new LrcLibLyricsProvider(new HttpClient(handler))
            .FindAsync(new("Track", "Artist", null, null));
        Assert.Equal(LyricsKind.Unavailable, result.Kind);
        Assert.Equal(2, handler.Requests.Count);
        Assert.DoesNotContain("album_name", handler.Requests[0].Query);
        Assert.DoesNotContain("duration", handler.Requests[0].Query);
    }

    [Fact]
    public void MatcherAcceptsExactIdentityWithARealisticDurationDifference()
    {
        var query = new LyricsQuery("Gidersin Araya", "Cash Flow", "Silah Gibi", TimeSpan.FromSeconds(146));
        LyricsCandidate[] candidates =
        [
            new(1, "Gidersin Araya", "Cash Flow", "Silah Gibi", TimeSpan.FromSeconds(150)),
            new(2, "Gidersin Araya", "RECO Cash Flow", "Silah Gibi", TimeSpan.FromSeconds(170))
        ];
        Assert.Equal(0, LyricsMatching.SelectBest(query, candidates));
        Assert.True(LyricsMatching.Score(query, candidates[0]) > 0.9);
    }

    [Fact]
    public void MatcherRejectsAmbiguousFuzzyResults()
    {
        var query = new LyricsQuery("Home", "Example", null, TimeSpan.FromSeconds(180));
        LyricsCandidate[] candidates =
        [
            new(1, "Home Live", "Example Band", null, TimeSpan.FromSeconds(180)),
            new(2, "Home Acoustic", "Example Band", null, TimeSpan.FromSeconds(180))
        ];
        Assert.Equal(-1, LyricsMatching.SelectBest(query, candidates, 0.7));
    }

    [Fact]
    public async Task ProviderFallsBackToSearchAndUsesTheBestCandidate()
    {
        var handler = new SequenceHandler(
            (HttpStatusCode.NotFound, "{}"),
            (HttpStatusCode.OK, """
                [
                  {"id":1,"trackName":"Gidersin Araya","artistName":"RECO Cash Flow","albumName":"Silah Gibi","duration":170,"instrumental":false,"plainLyrics":"Wrong","syncedLyrics":"[00:01] Wrong"},
                  {"id":2,"trackName":"GİDERSİN ARAYA","artistName":"Cash Flow","albumName":"Silah Gibi","duration":150,"instrumental":false,"plainLyrics":"Right","syncedLyrics":"[00:01] Right"}
                ]
                """));
        var result = await new LrcLibLyricsProvider(new HttpClient(handler)).FindAsync(
            new("Gidersin Araya", "Cash Flow", "Silah Gibi", TimeSpan.FromSeconds(146)));
        Assert.Equal(LyricsKind.Synced, result.Kind);
        Assert.Equal("Right", result.Lines.Single().Text);
        Assert.Contains("/api/get?", handler.Requests[0].AbsoluteUri);
        Assert.Contains("/api/search?", handler.Requests[1].AbsoluteUri);
    }

    [Fact]
    public async Task LrcLibSearchPrefersTimestampedDuplicateOverPlainExactResponse()
    {
        var handler = new SequenceHandler(
            (HttpStatusCode.OK, """{"id":1,"trackName":"Track","artistName":"Artist","duration":120,"instrumental":false,"plainLyrics":"Plain","syncedLyrics":null}"""),
            (HttpStatusCode.OK, """
                [
                  {"id":1,"trackName":"Track","artistName":"Artist","duration":120,"instrumental":false,"plainLyrics":"Plain","syncedLyrics":null},
                  {"id":2,"trackName":"Track","artistName":"Artist","duration":121,"instrumental":false,"plainLyrics":"Timed","syncedLyrics":"[00:01.00] Timed"}
                ]
                """));

        var result = await new LrcLibLyricsProvider(new HttpClient(handler))
            .FindAsync(new("Track", "Artist", null, TimeSpan.FromSeconds(120)));

        Assert.Equal(LyricsKind.Synced, result.Kind);
        Assert.Equal("Timed", result.Lines.Single().Text);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task BetterLyricsProviderParsesOnlyTimestampedTtml()
    {
        var handler = new StubHandler(HttpStatusCode.OK,
            """{"ttml":"<tt><body><div><p begin=\"00:01.500\">Synthetic line</p></div></body></tt>"}""");

        var result = await new BetterLyricsProvider(new HttpClient(handler), new Uri("https://lyrics.test/"))
            .FindAsync(new("Track", "Artist", "Album", TimeSpan.FromSeconds(100)));

        Assert.Equal(LyricsKind.Synced, result.Kind);
        Assert.Equal(1.5, result.Lines.Single().Start.TotalSeconds);
        Assert.Contains("getLyrics?", handler.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task AmllProviderValidatesSearchIdentityBeforeFetchingTtml()
    {
        var handler = new SequenceHandler(
            (HttpStatusCode.OK, """
                {"data":{"items":[{"id":42,"musicNames":["Track"],"artistNames":["Artist"],"albumNames":["Album"]}]}}
                """),
            (HttpStatusCode.OK, """
                {"data":{"lyrics":"<tt><body><div><p begin=\"2.25s\">Synthetic line</p></div></body></tt>"}}
                """));

        var result = await new AmllLyricsProvider(new HttpClient(handler), new Uri("https://lyrics.test/"))
            .FindAsync(new("Track", "Artist", "Album", TimeSpan.FromSeconds(100)));

        Assert.Equal(LyricsKind.Synced, result.Kind);
        Assert.Equal(2.25, result.Lines.Single().Start.TotalSeconds);
        Assert.Contains("id=42", handler.Requests[1].Query);
    }

    [Fact]
    public async Task AppleMusicProviderMatchesCatalogMetadataAndAcceptsOnlyTimedTtml()
    {
        var handler = new SequenceHandler(
            (HttpStatusCode.OK, """
                {"results":[{"artistId":7,"trackId":42,"trackName":"Track","artistName":"Artist","collectionName":"Album","trackTimeMillis":100000}]}
                """),
            (HttpStatusCode.OK, """
                {"type":"TTML","content":"<tt><body><div><p begin=\"3.5s\">Synthetic line</p></div></body></tt>"}
                """));

        var provider = new AppleMusicLyricsProvider(new HttpClient(handler),
            new Uri("https://catalog.test/"), new Uri("https://lyrics.test/"));
        var result = await provider.FindAsync(new("Track", "Artist", "Album", TimeSpan.FromSeconds(100)));

        Assert.Equal(LyricsKind.Synced, result.Kind);
        Assert.Equal(3.5, result.Lines.Single().Start.TotalSeconds);
        Assert.Contains("entity=song", handler.Requests[0].Query);
        Assert.Contains("apple-music/lyrics", handler.Requests[1].AbsoluteUri);
    }

    [Fact]
    public async Task AppleMusicProviderRejectsUntimedTtml()
    {
        var handler = new SequenceHandler(
            (HttpStatusCode.OK, """
                {"results":[{"artistId":7,"trackId":42,"trackName":"Track","artistName":"Artist","trackTimeMillis":100000}]}
                """),
            (HttpStatusCode.OK, """
                {"type":"TTML","content":"<tt><body><div><p>Synthetic line</p></div></body></tt>"}
                """));

        var result = await new AppleMusicLyricsProvider(new HttpClient(handler),
                new Uri("https://catalog.test/"), new Uri("https://lyrics.test/"))
            .FindAsync(new("Track", "Artist", null, TimeSpan.FromSeconds(100)));

        Assert.Equal(LyricsKind.Unavailable, result.Kind);
    }

    [Theory]
    [InlineData("{\"instrumental\":true,\"plainLyrics\":null,\"syncedLyrics\":null}", LyricsKind.Instrumental)]
    [InlineData("{\"instrumental\":false,\"plainLyrics\":\"Only plain\",\"syncedLyrics\":null}", LyricsKind.Plain)]
    public async Task ProviderDistinguishesInstrumentalAndPlainLyrics(string json, LyricsKind expected)
    {
        var provider = new LrcLibLyricsProvider(new HttpClient(new SequenceHandler(
            (HttpStatusCode.OK, json), (HttpStatusCode.OK, "[]"))));
        var result = await provider.FindAsync(new("Track", "Artist", "Album", TimeSpan.FromMinutes(3)));
        Assert.Equal(expected, result.Kind);
    }

    [Fact]
    public async Task FallbackProviderContinuesAfterUnavailableResult()
    {
        var provider = new FallbackLyricsProvider(
            new FixedProvider(LyricsDocument.Unavailable),
            new FixedProvider(new(LyricsKind.Plain, [], "Synthetic lyric")));
        var result = await provider.FindAsync(new("Track", "Artist", null, null));
        Assert.Equal(LyricsKind.Plain, result.Kind);
        Assert.Equal("Synthetic lyric", result.PlainText);
    }

    [Fact]
    public async Task FallbackProviderDoesNotLetPlainLyricsHideLaterSynchronizedResult()
    {
        var provider = new FallbackLyricsProvider(
            new FixedProvider(new(LyricsKind.Plain, [], "Synthetic plain")),
            new FixedProvider(new(LyricsKind.Synced, [new(TimeSpan.FromSeconds(1), "Synthetic timed")])));

        var result = await provider.FindAsync(new("Track", "Artist", null, null));

        Assert.Equal(LyricsKind.Synced, result.Kind);
        Assert.Equal("Synthetic timed", result.Lines.Single().Text);
    }

    [Fact]
    public async Task SlowLyricsSourceTimesOutWithoutBlockingTheNextSource()
    {
        var slow = new TimeoutLyricsProvider(new DelayedProvider(TimeSpan.FromSeconds(5)),
            TimeSpan.FromMilliseconds(40));
        var provider = new FallbackLyricsProvider(slow,
            new FixedProvider(new(LyricsKind.Synced, [new(TimeSpan.FromSeconds(1), "Recovered")])));

        var result = await provider.FindAsync(new("Track", "Artist", null, null));

        Assert.Equal(LyricsKind.Synced, result.Kind);
        Assert.Equal("Recovered", result.Lines.Single().Text);
    }

    [Fact]
    public async Task LyricsMemoryCacheAvoidsRepeatedNetworkLookup()
    {
        var inner = new CountingProvider(new(LyricsKind.Synced,
            [new(TimeSpan.FromSeconds(1), "Cached")]));
        var cache = new MemoryLyricsCacheProvider(inner);
        var query = new LyricsQuery("Track", "Artist", "Album", TimeSpan.FromSeconds(120));

        var first = await cache.FindAsync(query);
        var second = await cache.FindAsync(query);

        Assert.Same(first, second);
        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public async Task LyricsMemoryCacheNeverStoresACancelledLookup()
    {
        var inner = new CountingProvider(new(LyricsKind.Synced,
            [new(TimeSpan.FromSeconds(1), "Cached")]), TimeSpan.FromMilliseconds(100));
        var cache = new MemoryLyricsCacheProvider(inner);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            cache.FindAsync(new("Track", "Artist", null, null), cancellation.Token));

        var result = await cache.FindAsync(new("Track", "Artist", null, null));

        Assert.Equal(LyricsKind.Synced, result.Kind);
        Assert.Equal(2, inner.Calls);
    }

    [Fact]
    public async Task GeniusFallbackValidatesCatalogueIdentityAndExtractsPlainLyrics()
    {
        var handler = new SequenceHandler(
            (HttpStatusCode.OK, "window.__STATE__ = {artist_id\\\":4021697};"),
            (HttpStatusCode.OK, """
                {"response":{"songs":[
                  {"id":1,"title":"Wrong","artist_names":"KAVAK","url":"https://lyrics.test/wrong-lyrics"},
                  {"id":2,"title":"VAKKO","artist_names":"KAVAK & BAKAN","url":"https://lyrics.test/vakko-lyrics"}
                ]}}
                """),
            (HttpStatusCode.OK, """
                <div data-lyrics-container="true">Synthetic first<br/>Synthetic second</div>
                <div data-lyrics-container="true"><b>Synthetic third</b></div>
                """));
        var result = await new GeniusLyricsProvider(new HttpClient(handler), new Uri("https://lyrics.test/"))
            .FindAsync(new("VAKKO", "KAVAK, BAKAN", null, null));
        Assert.Equal(LyricsKind.Plain, result.Kind);
        Assert.Equal("Synthetic first\nSynthetic second\nSynthetic third", result.PlainText);
        Assert.Equal(3, handler.Requests.Count);
        Assert.Contains("artists/kavak", handler.Requests[0].AbsoluteUri);
        Assert.Contains("api/artists/4021697/songs", handler.Requests[1].AbsoluteUri);
    }

    [Fact]
    public async Task TurkishPlainLyricsFallbackValidatesPageAndExtractsText()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """
            <html><head><title>&quot;VAKKO&quot; şarkı sözleri - KAVAK şarkıları</title></head>
            <body><div class="lyrics-text">Synthetic one<br> Synthetic two</div></body></html>
            """);
        var result = await new SozMuzikLyricsProvider(new HttpClient(handler), new Uri("https://lyrics.test/"))
            .FindAsync(new("VAKKO", "KAVAK, BAKAN", null, null));
        Assert.Equal(LyricsKind.Plain, result.Kind);
        Assert.Equal("Synthetic one\n Synthetic two", result.PlainText);
        Assert.Contains("song/kavak-vakko", handler.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task SarkiAnaliziFallbackSearchesValidatesAndExtractsOnlyLyricsSection()
    {
        var handler = new SequenceHandler(
            (HttpStatusCode.OK, """
                <a href="https://lyrics.test/2025/08/kavak-fis.html">Şarkı Sözleri: KAVAK - fıs?</a>
                """),
            (HttpStatusCode.OK, """
                <div class="post-body entry-content" id="post-body-1">
                  <h2>GENEL ANALİZ</h2><div>Ignore this analysis</div>
                  <h2>Şarkı Sözleri: KAVAK - fıs?</h2>
                  <div>Synthetic first</div><div>Synthetic second<br/>Synthetic third</div>
                </div>
                """));

        var result = await new SarkiAnaliziLyricsProvider(new HttpClient(handler), new Uri("https://lyrics.test/"))
            .FindAsync(new("fıs?", "KAVAK", null, null));

        Assert.Equal(LyricsKind.Plain, result.Kind);
        Assert.Equal("Synthetic first\nSynthetic second\nSynthetic third", result.PlainText);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("search?q=", handler.Requests[0].AbsoluteUri);
    }

    [Fact]
    public async Task BbsFallbackUsesPostSearchAndRejectsArtistMixups()
    {
        var handler = new SequenceHandler(
            (HttpStatusCode.OK, """
                <a href="/sarki/42/sample-track">Sample Track</a>
                """),
            (HttpStatusCode.OK, """
                <html><head><title>Sample Track Sözleri - Sample Artist</title></head><body>
                <div class="tab-pane active" id="sarki-sozleri"><div class="well"><div class="row">
                <div class="col-md-6">Synthetic first<br/>Synthetic second</div>
                </div></div></div></body></html>
                """));

        var result = await new BbsLyricsProvider(new HttpClient(handler), new Uri("https://lyrics.test/"))
            .FindAsync(new("Sample Track", "Sample Artist", null, null));

        Assert.Equal(LyricsKind.Plain, result.Kind);
        Assert.Equal("Synthetic first\nSynthetic second", result.PlainText);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("POST", handler.Methods[0]);
    }

    private sealed class StubHandler(HttpStatusCode status, string json) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public string UserAgent { get; private set; } = "";
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            RequestUri = request.RequestUri;
            UserAgent = request.Headers.UserAgent.ToString();
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class FixedProvider(LyricsDocument result) : ILyricsProvider
    {
        public Task<LyricsDocument> FindAsync(LyricsQuery query, CancellationToken token = default) =>
            Task.FromResult(result);
    }

    private sealed class DelayedProvider(TimeSpan delay) : ILyricsProvider
    {
        public async Task<LyricsDocument> FindAsync(LyricsQuery query, CancellationToken token = default)
        {
            await Task.Delay(delay, token);
            return LyricsDocument.Unavailable;
        }
    }

    private sealed class CountingProvider(LyricsDocument result, TimeSpan? delay = null) : ILyricsProvider
    {
        public int Calls { get; private set; }
        public async Task<LyricsDocument> FindAsync(LyricsQuery query, CancellationToken token = default)
        {
            Calls++;
            if (delay is { } value) await Task.Delay(value, token);
            return result;
        }
    }

    private sealed class SequenceHandler(params (HttpStatusCode Status, string Json)[] responses) : HttpMessageHandler
    {
        private int _index;
        public List<Uri> Requests { get; } = [];
        public List<string> Methods { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Requests.Add(request.RequestUri!);
            Methods.Add(request.Method.Method);
            var response = responses[Math.Min(_index++, responses.Length - 1)];
            return Task.FromResult(new HttpResponseMessage(response.Status)
            {
                Content = new StringContent(response.Json, Encoding.UTF8, "application/json")
            });
        }
    }
}
