using System.Net;
using System.Text.RegularExpressions;
using Mediance.Core.Lyrics;

namespace Mediance.Lyrics;

public sealed partial class BbsLyricsProvider(HttpClient client, Uri? baseUri = null) : ILyricsProvider
{
    private readonly Uri _baseUri = baseUri ?? new("https://www.sarkisozleri.bbs.tr/");

    public async Task<LyricsDocument> FindAsync(LyricsQuery query, CancellationToken token = default)
    {
        using var search = new HttpRequestMessage(HttpMethod.Post, new Uri(_baseUri, "arama"))
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["q"] = $"{query.Artist} {query.Title}"
            })
        };
        search.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) Mediance/0.1");
        using var searchResponse = await client.SendAsync(search, HttpCompletionOption.ResponseHeadersRead, token);
        if (!searchResponse.IsSuccessStatusCode) return LyricsDocument.Unavailable;
        var searchHtml = await searchResponse.Content.ReadAsStringAsync(token);

        var links = ResultLinkRegex().Matches(searchHtml)
            .Select(match => (Url: new Uri(_baseUri, WebUtility.HtmlDecode(match.Groups["url"].Value)),
                Title: CleanText(match.Groups["label"].Value)))
            .Where(value => value.Title.Length > 0)
            .DistinctBy(value => value.Url)
            .ToArray();
        var best = LyricsMatching.SelectBest(query, links.Select((value, index) =>
            new LyricsCandidate(index, value.Title, query.Artist, null, null)).ToArray(), 0.92);
        if (best < 0) return LyricsDocument.Unavailable;

        using var pageRequest = new HttpRequestMessage(HttpMethod.Get, links[best].Url);
        pageRequest.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) Mediance/0.1");
        using var pageResponse = await client.SendAsync(pageRequest, HttpCompletionOption.ResponseHeadersRead, token);
        if (!pageResponse.IsSuccessStatusCode) return LyricsDocument.Unavailable;
        var html = await pageResponse.Content.ReadAsStringAsync(token);

        var identity = IdentityRegex().Match(html);
        if (!identity.Success) return LyricsDocument.Unavailable;
        var candidate = new LyricsCandidate(0, CleanText(identity.Groups["title"].Value),
            CleanText(identity.Groups["artist"].Value), null, null);
        if (LyricsMatching.SelectBest(query, [candidate], 0.88) < 0) return LyricsDocument.Unavailable;
        if (!TryExtractDiv(html, ActiveLyricsTabRegex(), out var tab) ||
            !TryExtractDiv(tab, LyricsColumnRegex(), out var column))
            return LyricsDocument.Unavailable;

        var lyrics = CleanText(BreakRegex().Replace(column, "\n")).Replace("\r", "").Trim();
        return lyrics.Length == 0 ? LyricsDocument.Unavailable : new(LyricsKind.Plain, [], lyrics);
    }

    private static bool TryExtractDiv(string html, Regex openingPattern, out string body)
    {
        body = "";
        var opening = openingPattern.Match(html);
        if (!opening.Success) return false;
        var depth = 1;
        foreach (Match tag in DivTagRegex().Matches(html, opening.Index + opening.Length))
        {
            depth += tag.Value.StartsWith("</", StringComparison.Ordinal) ? -1 : 1;
            if (depth != 0) continue;
            body = html.Substring(opening.Index + opening.Length,
                tag.Index - opening.Index - opening.Length);
            return true;
        }
        return false;
    }

    private static string CleanText(string html) =>
        WebUtility.HtmlDecode(TagRegex().Replace(html, "")).Replace("\u00a0", " ").Trim();

    [GeneratedRegex("<a\\b[^>]*href=[\"'](?<url>/sarki/\\d+/[^\"']+)[\"'][^>]*>(?<label>.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ResultLinkRegex();
    [GeneratedRegex("<title>\\s*(?<title>.*?)\\s+Sözleri\\s+-\\s+(?<artist>.*?)\\s*</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex IdentityRegex();
    [GeneratedRegex("<div\\b[^>]*class=[\"'][^\"']*tab-pane\\s+active[^\"']*[\"'][^>]*id=[\"']sarki-sozleri[\"'][^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ActiveLyricsTabRegex();
    [GeneratedRegex("<div\\b[^>]*class=[\"'][^\"']*col-md-6[^\"']*[\"'][^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex LyricsColumnRegex();
    [GeneratedRegex("<div\\b[^>]*>|</div\\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex DivTagRegex();
    [GeneratedRegex("<br\\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex BreakRegex();
    [GeneratedRegex("<[^>]+>", RegexOptions.Singleline)]
    private static partial Regex TagRegex();
}
