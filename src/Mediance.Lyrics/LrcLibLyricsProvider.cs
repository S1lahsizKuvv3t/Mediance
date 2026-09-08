using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Mediance.Core.Lyrics;

namespace Mediance.Lyrics;

public sealed class LrcLibLyricsProvider(HttpClient client) : ILyricsProvider
{
    public async Task<LyricsDocument> FindAsync(LyricsQuery query, CancellationToken token = default)
    {
        var exact = await GetExactAsync(query, token);
        var exactDocument = exact is null ? LyricsDocument.Unavailable : ToDocument(exact);
        if (exactDocument.Kind is LyricsKind.Synced or LyricsKind.Instrumental) return exactDocument;

        var searched = await SearchAsync(query, token);
        return searched.Kind == LyricsKind.Unavailable ? exactDocument : searched;
    }

    private async Task<Response?> GetExactAsync(LyricsQuery query, CancellationToken token)
    {
        var parameters = new List<string>
        {
            Pair("track_name", query.Title),
            Pair("artist_name", query.Artist)
        };
        if (!string.IsNullOrWhiteSpace(query.Album)) parameters.Add(Pair("album_name", query.Album));
        if (query.Duration is { TotalSeconds: > 0 and <= 3600 } duration)
            parameters.Add(Pair("duration", Math.Round(duration.TotalSeconds).ToString(CultureInfo.InvariantCulture)));

        using var request = new HttpRequestMessage(HttpMethod.Get,
            "https://lrclib.net/api/get?" + string.Join('&', parameters));
        request.Headers.UserAgent.ParseAdd("Mediance/0.1 (Windows; local prototype)");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Response>(cancellationToken: token)
            ?? throw new HttpRequestException("LRCLIB returned an empty response.");
    }

    private async Task<LyricsDocument> SearchAsync(LyricsQuery query, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://lrclib.net/api/search?{Pair("track_name", query.Title)}&{Pair("artist_name", query.Artist)}");
        request.Headers.UserAgent.ParseAdd("Mediance/0.1 (Windows; local prototype)");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        var values = await response.Content.ReadFromJsonAsync<Response[]>(cancellationToken: token) ?? [];
        var synced = values.Where(value => LrcParser.Parse(value.SyncedLyrics).Count > 0).ToArray();
        var bestSynced = SelectBest(query, synced);
        if (bestSynced >= 0) return ToDocument(synced[bestSynced]);

        var best = SelectBest(query, values);
        return best < 0 ? LyricsDocument.Unavailable : ToDocument(values[best]);
    }

    private static int SelectBest(LyricsQuery query, IReadOnlyList<Response> values) =>
        LyricsMatching.SelectBest(query, values.Select(value => new LyricsCandidate(value.Id,
            value.TrackName ?? "", value.ArtistName ?? "", value.AlbumName,
            value.Duration is > 0 and <= 3600 ? TimeSpan.FromSeconds(value.Duration.Value) : null)).ToArray());

    private static LyricsDocument ToDocument(Response value)
    {
        if (value.Instrumental) return LyricsDocument.Instrumental;
        var lines = LrcParser.Parse(value.SyncedLyrics);
        if (lines.Count > 0) return new(LyricsKind.Synced, lines, value.PlainLyrics);
        return string.IsNullOrWhiteSpace(value.PlainLyrics)
            ? LyricsDocument.Unavailable
            : new(LyricsKind.Plain, [], value.PlainLyrics);
    }

    private static string Pair(string name, string value) => $"{name}={Uri.EscapeDataString(value)}";

    private sealed record Response(
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("trackName")] string? TrackName,
        [property: JsonPropertyName("artistName")] string? ArtistName,
        [property: JsonPropertyName("albumName")] string? AlbumName,
        [property: JsonPropertyName("duration")] double? Duration,
        [property: JsonPropertyName("instrumental")] bool Instrumental,
        [property: JsonPropertyName("plainLyrics")] string? PlainLyrics,
        [property: JsonPropertyName("syncedLyrics")] string? SyncedLyrics);
}
