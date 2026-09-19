using Mediance.Core.Lyrics;

namespace Mediance.Lyrics;

public sealed class PlainProjectionLyricsProvider(ILyricsProvider inner) : ILyricsProvider
{
    public async Task<LyricsDocument> FindAsync(LyricsQuery query, CancellationToken token = default)
    {
        var document = await inner.FindAsync(query, token);
        if (document.Kind != LyricsKind.Synced) return document;
        var plainText = !string.IsNullOrWhiteSpace(document.PlainText)
            ? document.PlainText
            : string.Join('\n', document.Lines.Select(line => line.Text));
        var cleaned = PlainLyricsTimeline.Clean(plainText);
        return string.IsNullOrWhiteSpace(cleaned)
            ? LyricsDocument.Unavailable
            : new LyricsDocument(LyricsKind.Plain, [], cleaned);
    }
}
