using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Mediance.Core.Lyrics;

namespace Mediance.Lyrics;

public sealed class BetterLyricsProvider(HttpClient client, Uri? baseUri = null) : ILyricsProvider
{
    private readonly Uri _baseUri = baseUri ?? new("https://lyrics-api.boidu.dev/");

    public async Task<LyricsDocument> FindAsync(LyricsQuery query, CancellationToken token = default)
    {
        var parameters = new List<string>
        {
            Pair("s", query.Title), Pair("a", query.Artist)
        };
        if (!string.IsNullOrWhiteSpace(query.Album)) parameters.Add(Pair("al", query.Album));
        if (query.Duration is { TotalSeconds: > 0 and <= 3600 } duration)
            parameters.Add("d=" + Math.Round(duration.TotalSeconds).ToString(CultureInfo.InvariantCulture));

        using var request = new HttpRequestMessage(HttpMethod.Get,
            new Uri(_baseUri, "getLyrics?" + string.Join('&', parameters)));
        request.Headers.UserAgent.ParseAdd("Mediance/0.1 (Windows)");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Unauthorized or HttpStatusCode.TooManyRequests)
            return LyricsDocument.Unavailable;
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<Response>(cancellationToken: token);
        var lines = TtmlLyricsParser.Parse(payload?.Ttml);
        return lines.Count == 0 ? LyricsDocument.Unavailable : new(LyricsKind.Synced, lines);
    }

    private static string Pair(string name, string value) => $"{name}={Uri.EscapeDataString(value)}";
    private sealed record Response([property: JsonPropertyName("ttml")] string? Ttml);
}
