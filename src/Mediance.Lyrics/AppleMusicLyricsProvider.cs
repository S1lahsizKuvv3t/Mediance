using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Mediance.Core.Lyrics;

namespace Mediance.Lyrics;

public sealed class AppleMusicLyricsProvider(
    HttpClient client,
    Uri? catalogBaseUri = null,
    Uri? lyricsBaseUri = null) : ILyricsProvider
{
    private readonly Uri _catalogBaseUri = catalogBaseUri ?? new("https://itunes.apple.com/");
    private readonly Uri _lyricsBaseUri = lyricsBaseUri ?? new("https://lyrics.paxsenix.org/");

    public async Task<LyricsDocument> FindAsync(LyricsQuery query, CancellationToken token = default)
    {
        var songs = await SearchSongsAsync($"{query.Title} {query.Artist}", token);
        var match = SelectSong(query, songs);
        if (match is null)
        {
            var artist = await FindPrimaryArtistAsync(query.Artist, token);
            if (artist is null) return LyricsDocument.Unavailable;
            songs = await LookupArtistSongsAsync(artist.ArtistId, token);
            match = SelectSong(query, songs);
        }
        if (match is null) return LyricsDocument.Unavailable;

        using var request = CreateRequest(new Uri(_lyricsBaseUri,
            $"apple-music/lyrics?id={match.TrackId}&ttml=true"));
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Unauthorized or
            HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
            return LyricsDocument.Unavailable;
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<LyricsResponse>(cancellationToken: token);
        var lines = TtmlLyricsParser.Parse(payload?.Content);
        return lines.Count == 0 ? LyricsDocument.Unavailable : new(LyricsKind.Synced, lines);
    }

    private async Task<Item[]> SearchSongsAsync(string term, CancellationToken token)
    {
        var uri = new Uri(_catalogBaseUri,
            "search?term=" + Uri.EscapeDataString(term) + "&entity=song&limit=50&country=US");
        return (await ReadCatalogAsync(uri, token)).Where(item => item.TrackId > 0).ToArray();
    }

    private async Task<Item?> FindPrimaryArtistAsync(string artistName, CancellationToken token)
    {
        var primary = artistName.Split([",", "&", " feat.", " featuring "],
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)[0];
        var uri = new Uri(_catalogBaseUri, "search?term=" + Uri.EscapeDataString(primary) +
            "&entity=musicArtist&attribute=artistTerm&limit=15&country=US");
        var artists = await ReadCatalogAsync(uri, token);
        return artists
            .Where(item => item.ArtistId > 0 && !string.IsNullOrWhiteSpace(item.ArtistName))
            .OrderByDescending(item => ArtistScore(primary, item.ArtistName!))
            .FirstOrDefault(item => ArtistScore(primary, item.ArtistName!) >= 0.98);
    }

    private async Task<Item[]> LookupArtistSongsAsync(long artistId, CancellationToken token)
    {
        var uri = new Uri(_catalogBaseUri,
            $"lookup?id={artistId}&entity=song&limit=200&country=US");
        return (await ReadCatalogAsync(uri, token)).Where(item => item.TrackId > 0).ToArray();
    }

    private async Task<Item[]> ReadCatalogAsync(Uri uri, CancellationToken token)
    {
        using var request = CreateRequest(uri);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.TooManyRequests) return [];
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SearchResponse>(cancellationToken: token))?.Results ?? [];
    }

    private static Item? SelectSong(LyricsQuery query, IReadOnlyList<Item> songs)
    {
        var candidates = songs.Select(song => new LyricsCandidate(song.TrackId,
            song.TrackName ?? "", song.ArtistName ?? "", song.CollectionName,
            song.TrackTimeMillis is > 0 and <= 3_600_000
                ? TimeSpan.FromMilliseconds(song.TrackTimeMillis.Value)
                : null)).ToArray();
        var best = LyricsMatching.SelectBest(query, candidates);
        return best < 0 ? null : songs[best];
    }

    private static double ArtistScore(string requested, string candidate) =>
        LyricsMatching.Score(new("artist", requested, null, null),
            new(0, "artist", candidate, null, null));

    private static HttpRequestMessage CreateRequest(Uri uri)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.ParseAdd("Mediance/0.1 (Windows)");
        return request;
    }

    private sealed record SearchResponse([property: JsonPropertyName("results")] Item[] Results);
    private sealed class Item
    {
        [JsonPropertyName("artistId")]
        public long ArtistId { get; init; }
        [JsonPropertyName("trackId")]
        public long TrackId { get; init; }
        [JsonPropertyName("trackName")]
        public string? TrackName { get; init; }
        [JsonPropertyName("artistName")]
        public string? ArtistName { get; init; }
        [JsonPropertyName("collectionName")]
        public string? CollectionName { get; init; }
        [JsonPropertyName("trackTimeMillis")]
        public double? TrackTimeMillis { get; init; }
    }
    private sealed record LyricsResponse([property: JsonPropertyName("content")] string? Content);
}
