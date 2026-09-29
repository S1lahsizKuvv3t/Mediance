using Mediance.Core.Lyrics;

namespace Mediance.Lyrics;

public sealed class LyricsService(
    ILyricsProvider provider,
    LocalLyricsTimingStore? timingStore = null,
    TimeSpan? timeout = null) : IEditableLyricsService
{
    public async Task<LyricsDocument> FindAsync(LyricsQuery query, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query.Title);
        if (timingStore is not null)
        {
            var saved = await timingStore.LoadDocumentAsync(query, token);
            if (saved is not null) return saved;
        }
        using var limited = CancellationTokenSource.CreateLinkedTokenSource(token);
        limited.CancelAfter(timeout ?? TimeSpan.FromSeconds(15));
        var document = await provider.FindAsync(query, limited.Token);
        if (document.Kind == LyricsKind.Synced && timingStore is not null)
        {
            // Disk failure must not hide valid source lyrics. Manual/automatic saves
            // still propagate write failures so the UI never reports a false save.
            try { await timingStore.SaveDocumentAsync(query, document, limited.Token); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (ArgumentException) { }
        }
        if (document.Kind != LyricsKind.Plain) return document;

        var cleaned = PlainLyricsTimeline.Clean(document.PlainText);
        if (string.IsNullOrWhiteSpace(cleaned)) return LyricsDocument.Unavailable;
        if (timingStore is not null)
        {
            var timings = await timingStore.LoadAsync(query, cleaned, limited.Token);
            if (timings is not null)
            {
                // Schema 2 stored hashes only. Hydrate its text once, retaining timing.
                await timingStore.SaveAsync(query, cleaned, timings, limited.Token);
                return ToSynced(cleaned, timings);
            }
        }
        return new LyricsDocument(LyricsKind.Plain, [], cleaned);
    }

    public async Task<LyricsDocument> SaveTimingAsync(LyricsQuery query, string plainText,
        IReadOnlyList<TimeSpan> timings, CancellationToken token = default)
    {
        if (timingStore is null) throw new InvalidOperationException("No lyrics timing store is configured.");
        var cleaned = PlainLyricsTimeline.Clean(plainText);
        await timingStore.SaveAsync(query, cleaned, timings, token);
        return ToSynced(cleaned, timings);
    }

    private static LyricsDocument ToSynced(string plainText, IReadOnlyList<TimeSpan> timings)
    {
        var lines = plainText.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length != timings.Count) throw new ArgumentException("Every lyric line needs one timestamp.");
        return new(LyricsKind.Synced,
            lines.Select((line, index) => new LyricLine(timings[index], line)).ToArray(), plainText, true);
    }
}
