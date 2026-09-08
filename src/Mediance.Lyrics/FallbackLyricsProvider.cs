using Mediance.Core.Lyrics;

namespace Mediance.Lyrics;

public sealed class FallbackLyricsProvider(params ILyricsProvider[] providers) : ILyricsProvider
{
    public async Task<LyricsDocument> FindAsync(LyricsQuery query, CancellationToken token = default)
    {
        LyricsDocument? plainFallback = null;
        foreach (var provider in providers)
        {
            try
            {
                var result = await provider.FindAsync(query, token);
                if (result.Kind is LyricsKind.Synced or LyricsKind.Instrumental) return result;
                if (result.Kind == LyricsKind.Plain) plainFallback ??= result;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception) { }
        }
        return plainFallback ?? LyricsDocument.Unavailable;
    }
}
