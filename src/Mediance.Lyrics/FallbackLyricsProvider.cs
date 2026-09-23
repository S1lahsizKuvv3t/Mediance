using Mediance.Core.Lyrics;

namespace Mediance.Lyrics;

public sealed class FallbackLyricsProvider(params ILyricsProvider[] providers) : ILyricsProvider
{
    public async Task<LyricsDocument> FindAsync(LyricsQuery query, CancellationToken token = default)
    {
        var lookups = providers.Select(provider => FindSafelyAsync(provider, query, token)).ToArray();
        var results = await Task.WhenAll(lookups);

        LyricsDocument? plainFallback = null;
        foreach (var result in results)
        {
            if (result.Kind is LyricsKind.Synced or LyricsKind.Instrumental) return result;
            if (result.Kind == LyricsKind.Plain) plainFallback ??= result;
        }
        return plainFallback ?? LyricsDocument.Unavailable;
    }

    private static async Task<LyricsDocument> FindSafelyAsync(ILyricsProvider provider, LyricsQuery query,
        CancellationToken token)
    {
        try { return await provider.FindAsync(query, token); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception) { return LyricsDocument.Unavailable; }
    }
}
