using Mediance.Core.Lyrics;

namespace Mediance.Lyrics;

/// <summary>Bounds one source so a slow endpoint cannot consume the whole fallback budget.</summary>
public sealed class TimeoutLyricsProvider(ILyricsProvider inner, TimeSpan timeout) : ILyricsProvider
{
    public async Task<LyricsDocument> FindAsync(LyricsQuery query, CancellationToken token = default)
    {
        if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
        using var limited = CancellationTokenSource.CreateLinkedTokenSource(token);
        limited.CancelAfter(timeout);
        try { return await inner.FindAsync(query, limited.Token); }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            return LyricsDocument.Unavailable;
        }
    }
}
