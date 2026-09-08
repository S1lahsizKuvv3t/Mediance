using Mediance.Core.Lyrics;

namespace Mediance.Lyrics;

public sealed class FirstAvailableLyricsProvider(params ILyricsProvider[] providers) : ILyricsProvider
{
    public async Task<LyricsDocument> FindAsync(LyricsQuery query, CancellationToken token = default)
    {
        foreach (var provider in providers)
        {
            try
            {
                var result = await provider.FindAsync(query, token);
                if (result.Kind != LyricsKind.Unavailable) return result;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception) { }
        }
        return LyricsDocument.Unavailable;
    }
}
