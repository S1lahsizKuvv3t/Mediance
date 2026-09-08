using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Mediance.Core.Lyrics;

namespace Mediance.Lyrics;

public sealed partial class SozMuzikLyricsProvider(HttpClient client, Uri? baseUri = null) : ILyricsProvider
{
    private readonly Uri _baseUri = baseUri ?? new("https://sozmuzik.net/");

    public async Task<LyricsDocument> FindAsync(LyricsQuery query, CancellationToken token = default)
    {
        var primaryArtist = query.Artist.Split([',', '&'], 2)[0].Trim();
        var stem = $"{Slug(primaryArtist)}-{Slug(query.Title)}";
        var html = await GetPageAsync(stem, token);
        if (html.Length == 0 && query.Artist.Contains("BAKAN", StringComparison.OrdinalIgnoreCase))
            html = await GetPageAsync(stem + "-feat-bakan", token);
        if (html.Length == 0) return LyricsDocument.Unavailable;

        var identity = IdentityRegex().Match(html);
        if (!identity.Success) return LyricsDocument.Unavailable;
        var candidate = new LyricsCandidate(0,
            WebUtility.HtmlDecode(identity.Groups[1].Value),
            WebUtility.HtmlDecode(identity.Groups[2].Value), null, null);
        if (LyricsMatching.SelectBest(query, [candidate], 0.86) < 0) return LyricsDocument.Unavailable;

        var match = LyricsRegex().Match(html);
        if (!match.Success) return LyricsDocument.Unavailable;
        var lyrics = WebUtility.HtmlDecode(TagRegex().Replace(
            BreakRegex().Replace(match.Groups[1].Value, "\n"), ""))
            .Replace("\r", "").Trim();
        return lyrics.Length == 0 ? LyricsDocument.Unavailable : new(LyricsKind.Plain, [], lyrics);
    }

    private async Task<string> GetPageAsync(string path, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_baseUri, "song/" + path));
        request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) Mediance/0.1");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        if (!response.IsSuccessStatusCode) return "";
        return await response.Content.ReadAsStringAsync(token);
    }

    private static string Slug(string value)
    {
        value = value.Replace('ı', 'i').Replace('İ', 'I').Replace('ş', 's').Replace('Ş', 'S')
            .Replace('ğ', 'g').Replace('Ğ', 'G').Replace('ç', 'c').Replace('Ç', 'C')
            .Replace('ö', 'o').Replace('Ö', 'O').Replace('ü', 'u').Replace('Ü', 'U');
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(decomposed.Length);
        var dash = false;
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(character)) { result.Append(char.ToLowerInvariant(character)); dash = false; }
            else if (!dash && result.Length > 0) { result.Append('-'); dash = true; }
        }
        return result.ToString().Trim('-');
    }

    [GeneratedRegex("<title>\\s*(?:&quot;|\")(.+?)(?:&quot;|\")\\s+şarkı sözleri\\s+-\\s+(.+?)\\s+şarkıları\\s*</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex IdentityRegex();
    [GeneratedRegex("<div[^>]*class=\"[^\"]*lyrics-text[^\"]*\"[^>]*>(.*?)</div>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex LyricsRegex();
    [GeneratedRegex("<br\\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex BreakRegex();
    [GeneratedRegex("<[^>]+>", RegexOptions.Singleline)]
    private static partial Regex TagRegex();
}
