using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Mediance.Core.Lyrics;

namespace Mediance.Lyrics;

public sealed partial class GeniusLyricsProvider(HttpClient client, Uri? baseUri = null, Action<string>? diagnostic = null) : ILyricsProvider
{
    private readonly Uri _baseUri = baseUri ?? new("https://genius.com/");

    public async Task<LyricsDocument> FindAsync(LyricsQuery query, CancellationToken token = default)
    {
        var artistSlug = Slug(PrimaryArtist(query.Artist));
        if (artistSlug.Length == 0) return LyricsDocument.Unavailable;

        var artistPage = await GetTextAsync(new Uri(_baseUri, $"artists/{artistSlug}"), token);
        diagnostic?.Invoke($"artist-page:{artistPage.Length}");
        var idMatch = ArtistIdRegex().Match(artistPage);
        if (!idMatch.Success || !long.TryParse(idMatch.Groups[1].Value, out var artistId))
            return LyricsDocument.Unavailable;

        var catalogueUri = new Uri(_baseUri, $"api/artists/{artistId}/songs?page=1&per_page=50&sort=title");
        using var catalogueRequest = Request(catalogueUri, new Uri(_baseUri, $"artists/{artistSlug}"));
        using var catalogueResponse = await client.SendAsync(catalogueRequest, HttpCompletionOption.ResponseHeadersRead, token);
        if (!catalogueResponse.IsSuccessStatusCode) return LyricsDocument.Unavailable;
        var catalogue = await catalogueResponse.Content.ReadFromJsonAsync<Catalogue>(cancellationToken: token);
        var songs = catalogue?.Response?.Songs ?? [];
        diagnostic?.Invoke($"catalogue:{songs.Length}");
        var candidates = songs.Select(song => new LyricsCandidate(song.Id, song.Title ?? "",
            song.ArtistNames ?? "", null, null)).ToArray();
        var best = LyricsMatching.SelectBest(query, candidates, 0.86);
        diagnostic?.Invoke($"candidate:{best}");
        if (best < 0 || !Uri.TryCreate(songs[best].Url, UriKind.Absolute, out var songUri))
            return LyricsDocument.Unavailable;

        var html = await GetTextAsync(songUri, token);
        diagnostic?.Invoke($"song-page:{html.Length}");
        var lyrics = ExtractPreloadedLyrics(html, diagnostic);
        diagnostic?.Invoke($"preloaded-lyrics:{lyrics.Length}");
        if (lyrics.Length == 0)
        {
            var containers = LyricsContainerRegex().Matches(html);
            lyrics = string.Join('\n', containers.Select(match => CleanHtml(match.Groups[1].Value))
                .Where(text => !string.IsNullOrWhiteSpace(text))).Trim();
        }
        return lyrics.Length == 0 ? LyricsDocument.Unavailable : new(LyricsKind.Plain, [], lyrics);
    }

    private async Task<string> GetTextAsync(Uri uri, CancellationToken token)
    {
        using var request = Request(uri);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        if (response.StatusCode == HttpStatusCode.NotFound) return "";
        if (!response.IsSuccessStatusCode) return "";
        return await response.Content.ReadAsStringAsync(token);
    }

    private static HttpRequestMessage Request(Uri uri, Uri? referer = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Mediance/0.1");
        request.Headers.AcceptLanguage.ParseAdd("tr-TR,tr;q=0.9,en;q=0.8");
        if (referer is not null) request.Headers.Referrer = referer;
        return request;
    }

    private static string PrimaryArtist(string artist) => artist.Split([',', '&'], 2)[0].Trim();

    private static string Slug(string value)
    {
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

    private static string CleanHtml(string html)
    {
        html = BreakRegex().Replace(html, "\n");
        html = TagRegex().Replace(html, "");
        return WebUtility.HtmlDecode(html).Replace("\r", "").Trim();
    }

    private static string ExtractPreloadedLyrics(string html, Action<string>? diagnostic = null)
    {
        var match = PreloadedStateRegex().Match(html);
        diagnostic?.Invoke($"preloaded-state:{match.Success}:{(match.Success ? match.Groups[1].Value.Length : 0)}");
        if (!match.Success) return "";
        try
        {
            var encoded = "\"" + match.Groups[1].Value.Replace("\\'", "'") + "\"";
            var stateJson = JsonSerializer.Deserialize<string>(encoded);
            diagnostic?.Invoke($"decoded-state:{stateJson?.Length ?? 0}");
            if (string.IsNullOrWhiteSpace(stateJson)) return "";
            using var state = JsonDocument.Parse(stateJson);
            var lyricsHtml = FindLyricsHtml(state.RootElement, diagnostic);
            diagnostic?.Invoke($"lyrics-html:{lyricsHtml?.Length ?? 0}");
            return lyricsHtml is { Length: > 0 }
                ? CleanHtml(lyricsHtml)
                : "";
        }
        catch (JsonException ex) { diagnostic?.Invoke($"state-json-error:{ex.BytePositionInLine}"); return ""; }
    }

    private static string? FindLyricsHtml(JsonElement element, Action<string>? diagnostic = null)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("lyricsData", out var lyricsData))
            {
                diagnostic?.Invoke($"lyrics-data-kind:{lyricsData.ValueKind}");
                if (lyricsData.ValueKind == JsonValueKind.Object)
                    diagnostic?.Invoke("lyrics-data-keys:" + string.Join(',', lyricsData.EnumerateObject().Select(x => x.Name)));
                if (lyricsData.ValueKind == JsonValueKind.Object && lyricsData.TryGetProperty("body", out var body))
                {
                    diagnostic?.Invoke($"lyrics-body-kind:{body.ValueKind}");
                    if (body.ValueKind == JsonValueKind.Object)
                        diagnostic?.Invoke("lyrics-body-keys:" + string.Join(',', body.EnumerateObject().Select(x => x.Name)));
                    if (body.ValueKind == JsonValueKind.Object && body.TryGetProperty("tag", out var tag) && tag.ValueKind == JsonValueKind.String)
                        diagnostic?.Invoke($"lyrics-body-tag:{tag.GetString()}");
                    if (body.ValueKind == JsonValueKind.Object && body.TryGetProperty("html", out var lyricHtml) &&
                        lyricHtml.ValueKind == JsonValueKind.String) return lyricHtml.GetString();
                    if (body.ValueKind == JsonValueKind.String) return body.GetString();
                }
            }
            foreach (var property in element.EnumerateObject())
                if (FindLyricsHtml(property.Value, diagnostic) is { } found) return found;
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var child in element.EnumerateArray())
                if (FindLyricsHtml(child, diagnostic) is { } found) return found;
        return null;
    }

    [GeneratedRegex("artist_id(?:\\\\\"|&quot;|\")?\\s*:\\s*(\\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex ArtistIdRegex();
    [GeneratedRegex("data-lyrics-container=\"true\"[^>]*>(.*?)</div>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex LyricsContainerRegex();
    [GeneratedRegex("window\\.__PRELOADED_STATE__\\s*=\\s*JSON\\.parse\\('(.+?)'\\);", RegexOptions.Singleline)]
    private static partial Regex PreloadedStateRegex();
    [GeneratedRegex("<br\\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex BreakRegex();
    [GeneratedRegex("<[^>]+>", RegexOptions.Singleline)]
    private static partial Regex TagRegex();

    private sealed record Catalogue([property: JsonPropertyName("response")] CatalogueResponse? Response);
    private sealed record CatalogueResponse([property: JsonPropertyName("songs")] Song[]? Songs);
    private sealed record Song(
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("artist_names")] string? ArtistNames,
        [property: JsonPropertyName("url")] string? Url);
}
