using System.Text.Json.Nodes;
using Mediance.Core.Lyrics;
using Mediance.Lyrics;
using Xunit;

namespace Mediance.Core.Tests;

public sealed class LyricsLibraryTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "Mediance-library-" + Guid.NewGuid().ToString("N"));
    private string PathName => Path.Combine(_directory, "lyrics-timing.json");
    private static LyricsQuery Query => new("Example", "Artist", null, TimeSpan.FromSeconds(90));

    [Fact]
    public async Task SourceLyricsAreSavedWithProvenanceAndRepeatedTimestamps()
    {
        var source = new LyricsDocument(LyricsKind.Synced,
            [new(TimeSpan.FromSeconds(2), "First"), new(TimeSpan.FromSeconds(2), "Together"),
             new(TimeSpan.FromSeconds(10), "Next")]);
        var online = new CountingProvider(source);
        var first = await new LyricsService(online, new(PathName)).FindAsync(Query);
        Assert.Equal(source, first);
        Assert.Equal(1, online.Calls);
        var offline = new CountingProvider(LyricsDocument.Unavailable);
        var saved = await new LyricsService(offline, new(PathName)).FindAsync(Query);
        Assert.Equal(source.Lines, saved.Lines);
        Assert.False(saved.IsUserTimed);
        Assert.Equal(0, offline.Calls);
    }

    [Fact]
    public async Task LegacyHashesAreHydratedOnceWithoutLosingTiming()
    {
        var store = new LocalLyricsTimingStore(PathName);
        await store.SaveAsync(Query, "First\nSecond", [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(9)]);
        var legacy = JsonNode.Parse(await File.ReadAllTextAsync(PathName))!;
        legacy["schemaVersion"] = 2;
        foreach (var entry in legacy["entries"]!.AsArray())
        {
            entry!.AsObject().Remove("textLines");
            entry.AsObject().Remove("isUserTimed");
        }
        await File.WriteAllTextAsync(PathName, legacy.ToJsonString());
        var provider = new CountingProvider(new(LyricsKind.Plain, [], "First\nSecond"));
        var restored = await new LyricsService(provider, new(PathName)).FindAsync(Query);
        Assert.True(restored.IsUserTimed);
        Assert.Equal(9, restored.Lines[1].Start.TotalSeconds);
        Assert.Equal(1, provider.Calls);
        var reopened = await new LyricsService(provider, new(PathName)).FindAsync(Query);
        Assert.Equal(restored.Lines, reopened.Lines);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task DifferentArtistOrRecordingDoesNotReuseTiming()
    {
        var store = new LocalLyricsTimingStore(PathName);
        await store.SaveAsync(Query, "First", [TimeSpan.FromSeconds(2)]);
        Assert.Null(await store.LoadDocumentAsync(Query with { Artist = "Someone else" }));
        Assert.Null(await store.LoadDocumentAsync(Query with { Duration = TimeSpan.FromSeconds(180) }));
        Assert.NotNull(await store.LoadDocumentAsync(Query with { Title = "EXAMPLE!" }));
        var longer = Query with { Duration = TimeSpan.FromSeconds(180) };
        await store.SaveAsync(longer, "First", [TimeSpan.FromSeconds(20)]);
        Assert.Equal(2, (await store.LoadDocumentAsync(Query))!.Lines[0].Start.TotalSeconds);
        Assert.Equal(20, (await store.LoadDocumentAsync(longer))!.Lines[0].Start.TotalSeconds);
        var entries = await store.ListAsync();
        Assert.Equal(2, entries.Count);
        Assert.Equal(2, entries.Select(entry => entry.Id).Distinct().Count());
    }

    [Fact]
    public async Task AddingSongDoesNotEvictOlderCompletedEntries()
    {
        var store = new LocalLyricsTimingStore(PathName);
        await store.SaveAsync(Query, "First", [TimeSpan.Zero]);
        var file = JsonNode.Parse(await File.ReadAllTextAsync(PathName))!;
        var seed = file["entries"]![0]!.DeepClone();
        var entries = file["entries"]!.AsArray();
        entries.Clear();
        for (var index = 0; index < 500; index++)
        {
            var entry = seed.DeepClone();
            entry["trackKey"] = "synthetic-key-" + index;
            entries.Add(entry);
        }
        await File.WriteAllTextAsync(PathName, file.ToJsonString());
        await store.SaveAsync(Query, "First", [TimeSpan.Zero]);
        Assert.Equal(501, (await new LocalLyricsTimingStore(PathName).ListAsync()).Count);
    }

    [Fact]
    public async Task FutureLibraryIsPreservedRatherThanOverwritten()
    {
        Directory.CreateDirectory(_directory);
        const string future = "{\"schemaVersion\":99,\"entries\":[]}";
        await File.WriteAllTextAsync(PathName, future);
        await Assert.ThrowsAsync<NotSupportedException>(() => new LocalLyricsTimingStore(PathName)
            .SaveAsync(Query, "First", [TimeSpan.Zero]));
        Assert.Equal(future, await File.ReadAllTextAsync(PathName));
    }

    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
    private sealed class CountingProvider(LyricsDocument document) : ILyricsProvider
    {
        public int Calls { get; private set; }
        public Task<LyricsDocument> FindAsync(LyricsQuery query, CancellationToken token = default)
        { Calls++; return Task.FromResult(document); }
    }
}
