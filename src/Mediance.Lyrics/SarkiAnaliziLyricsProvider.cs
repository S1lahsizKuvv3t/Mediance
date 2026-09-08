using System.Net;
using System.Text.RegularExpressions;
using Mediance.Core.Lyrics;

namespace Mediance.Lyrics;

public sealed partial class SarkiAnaliziLyricsProvider(HttpClient client, Uri? baseUri = null) : ILyricsProvider
{
    private readonly Uri _baseUri = baseUri ?? new("https://www.sarkianalizi.com/");

    public async Task<LyricsDocument> FindAsync(LyricsQuery query, CancellationToken token = default)
    {
        var searchHtml = await SendAsync(new Uri(_baseUri,
            "search?q=" + Uri.EscapeDataString($"{query.Artist} {query.Title}")), token);
        if (searchHtml.Length == 0) return LyricsDocument.Unavailable;

        var candidates = SearchLinkRegex().Matches(searchHtml)
            .Select(match =>
            {
                var label = CleanText(match.Groups["label"].Value);
                return TryParseIdentity(label, out var title, out var artist)
                    ? (Url: new Uri(_baseUri, WebUtility.HtmlDecode(match.Groups["url"].Value)),
                        Candidate: new LyricsCandidate(0, title, artist, null, null))
                    : default;
            })
            .Where(value => value.Url is not null)
            .DistinctBy(value => value.Url)
            .ToArray();
        var best = LyricsMatching.SelectBest(query, candidates.Select(value => value.Candidate).ToArray(), 0.86);
        if (best < 0) return LyricsDocument.Unavailable;

        var pageHtml = await SendAsync(candidates[best].Url, token);
        if (pageHtml.Length == 0 || !TryExtractPostBody(pageHtml, out var postBody))
            return LyricsDocument.Unavailable;

        var headings = HeadingRegex().Matches(postBody);
        Match? lyricsHeading = null;
        for (var index = headings.Count - 1; index >= 0; index--)
        {
            var heading = CleanText(headings[index].Groups["body"].Value);
            if (!TryParseIdentity(heading, out var title, out var artist)) continue;
            var candidate = new LyricsCandidate(0, title, artist, null, null);
            if (LyricsMatching.SelectBest(query, [candidate], 0.86) < 0) continue;
            lyricsHeading = headings[index];
            break;
        }
        if (lyricsHeading is null) return LyricsDocument.Unavailable;

        var section = postBody[(lyricsHeading.Index + lyricsHeading.Length)..];
        var lyrics = CleanLyrics(section);
        return lyrics.Length == 0 ? LyricsDocument.Unavailable : new(LyricsKind.Plain, [], lyrics);
    }

    private async Task<string> SendAsync(Uri uri, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) Mediance/0.1");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        if (!response.IsSuccessStatusCode) return "";
        return await response.Content.ReadAsStringAsync(token);
    }

    private static bool TryExtractPostBody(string html, out string body)
    {
        body = "";
        var opening = PostBodyRegex().Match(html);
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

    private static bool TryParseIdentity(string value, out string title, out string artist)
    {
        value = value.Trim();
        value = PrefixRegex().Replace(value, "");
        value = SuffixRegex().Replace(value, "");
        var parts = DashRegex().Split(value, 2);
        if (parts.Length != 2)
        {
            title = artist = "";
            return false;
        }
        artist = parts[0].Trim();
        title = parts[1].Trim();
        return title.Length > 0 && artist.Length > 0;
    }

    private static string CleanLyrics(string html)
    {
        html = ScriptRegex().Replace(html, "");
        html = LineBreakRegex().Replace(html, "\n");
        var text = CleanText(html).Replace('\r', '\n');
        return string.Join('\n', text.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
    }

    private static string CleanText(string html) =>
        WebUtility.HtmlDecode(TagRegex().Replace(html, "")).Replace("\u00a0", " ").Trim();

    [GeneratedRegex("<a\\b[^>]*href=[\"'](?<url>[^\"']+\\.html)[\"'][^>]*>(?<label>.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex SearchLinkRegex();
    [GeneratedRegex("<div\\b[^>]*class=[\"'][^\"']*post-body[^\"']*[\"'][^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex PostBodyRegex();
    [GeneratedRegex("<div\\b[^>]*>|</div\\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex DivTagRegex();
    [GeneratedRegex("<h2\\b[^>]*>(?<body>.*?)</h2>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex HeadingRegex();
    [GeneratedRegex("^\\s*Şarkı\\s+Sözleri\\s*:\\s*", RegexOptions.IgnoreCase)]
    private static partial Regex PrefixRegex();
    [GeneratedRegex("\\s+Şarkı\\s+Sözleri(?:\\s+Analizi)?\\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex SuffixRegex();
    [GeneratedRegex("\\s+[–—-]\\s+")]
    private static partial Regex DashRegex();
    [GeneratedRegex("<(?:script|style)\\b[^>]*>.*?</(?:script|style)>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ScriptRegex();
    [GeneratedRegex("<br\\s*/?>|</(?:div|p|li)\\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex LineBreakRegex();
    [GeneratedRegex("<[^>]+>", RegexOptions.Singleline)]
    private static partial Regex TagRegex();
}
