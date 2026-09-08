using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Mediance.Core.Lyrics;

namespace Mediance.Lyrics;

public sealed class AmllLyricsProvider(HttpClient client, Uri? baseUri = null) : ILyricsProvider
{
    private readonly Uri _baseUri = baseUri ?? new("https://api.amll.dev/");

    public async Task<LyricsDocument> FindAsync(LyricsQuery query, CancellationToken token = default)
    {
        var uri = new Uri(_baseUri, "v1/lyrics/search?musicName=" + Uri.EscapeDataString(query.Title) +
            "&artistName=" + Uri.EscapeDataString(query.Artist) + "&pageSize=10");
        using var searchRequest = new HttpRequestMessage(HttpMethod.Get, uri);
        searchRequest.Headers.UserAgent.ParseAdd("Mediance/0.1 (Windows)");
        using var searchResponse = await client.SendAsync(searchRequest, HttpCompletionOption.ResponseHeadersRead, token);
        if (searchResponse.StatusCode == HttpStatusCode.NotFound) return LyricsDocument.Unavailable;
        searchResponse.EnsureSuccessStatusCode();
        var search = await searchResponse.Content.ReadFromJsonAsync<SearchEnvelope>(cancellationToken: token);
        var items = search?.Data?.Items ?? [];
        var candidates = items.Select(item => new LyricsCandidate(item.Id,
            item.MusicNames.FirstOrDefault() ?? "", string.Join(", ", item.ArtistNames),
            item.AlbumNames.FirstOrDefault(), null)).ToArray();
        var best = LyricsMatching.SelectBest(query, candidates, 0.86);
        if (best < 0) return LyricsDocument.Unavailable;

        using var getRequest = new HttpRequestMessage(HttpMethod.Get,
            new Uri(_baseUri, "v1/lyrics/get?id=" + items[best].Id));
        getRequest.Headers.UserAgent.ParseAdd("Mediance/0.1 (Windows)");
        using var getResponse = await client.SendAsync(getRequest, HttpCompletionOption.ResponseHeadersRead, token);
        if (getResponse.StatusCode == HttpStatusCode.NotFound) return LyricsDocument.Unavailable;
        getResponse.EnsureSuccessStatusCode();
        var result = await getResponse.Content.ReadFromJsonAsync<GetEnvelope>(cancellationToken: token);
        var lines = TtmlLyricsParser.Parse(result?.Data?.Lyrics);
        return lines.Count == 0 ? LyricsDocument.Unavailable : new(LyricsKind.Synced, lines);
    }

    private sealed record SearchEnvelope([property: JsonPropertyName("data")] SearchData? Data);
    private sealed class SearchData
    {
        [JsonPropertyName("items")]
        public Item[] Items { get; init; } = [];
    }
    private sealed class Item
    {
        [JsonPropertyName("id")]
        public long Id { get; init; }
        [JsonPropertyName("musicNames")]
        public string[] MusicNames { get; init; } = [];
        [JsonPropertyName("artistNames")]
        public string[] ArtistNames { get; init; } = [];
        [JsonPropertyName("albumNames")]
        public string[] AlbumNames { get; init; } = [];
    }
    private sealed record GetEnvelope([property: JsonPropertyName("data")] GetData? Data);
    private sealed record GetData([property: JsonPropertyName("lyrics")] string? Lyrics);
}
