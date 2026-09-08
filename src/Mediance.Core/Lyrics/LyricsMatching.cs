using System.Globalization;
using System.Text;

namespace Mediance.Core.Lyrics;

public sealed record LyricsCandidate(
    long Id,
    string TrackName,
    string ArtistName,
    string? AlbumName,
    TimeSpan? Duration);

public static class LyricsMatching
{
    public static int SelectBest(LyricsQuery query, IReadOnlyList<LyricsCandidate> candidates,
        double minimumConfidence = 0.84)
    {
        if (candidates.Count == 0) return -1;
        var scored = candidates.Select((candidate, index) => new
        {
            Index = index,
            Score = Score(query, candidate),
            ExactIdentity = Normalize(query.Title) == Normalize(candidate.TrackName) &&
                            Normalize(query.Artist) == Normalize(candidate.ArtistName)
        }).OrderByDescending(x => x.Score).ToArray();

        var best = scored[0];
        if (best.Score < minimumConfidence) return -1;
        // Exact title + artist is safe even when duplicate releases tie. For a
        // fuzzy identity, require a clear lead to avoid displaying another song.
        if (!best.ExactIdentity && scored.Length > 1 && best.Score - scored[1].Score < 0.05) return -1;
        return best.Index;
    }

    public static double Score(LyricsQuery query, LyricsCandidate candidate)
    {
        var weighted = Similarity(query.Title, candidate.TrackName) * 0.40
                     + Similarity(query.Artist, candidate.ArtistName) * 0.35;
        var totalWeight = 0.75;

        if (query.Duration is { TotalSeconds: > 0 } requested && candidate.Duration is { TotalSeconds: > 0 } found)
        {
            var difference = Math.Abs(requested.TotalSeconds - found.TotalSeconds);
            weighted += Math.Max(0, 1 - difference / 12) * 0.20;
            totalWeight += 0.20;
        }
        if (!string.IsNullOrWhiteSpace(query.Album) && !string.IsNullOrWhiteSpace(candidate.AlbumName))
        {
            weighted += Similarity(query.Album, candidate.AlbumName) * 0.05;
            totalWeight += 0.05;
        }
        return weighted / totalWeight;
    }

    private static double Similarity(string? left, string? right)
    {
        var a = Normalize(left);
        var b = Normalize(right);
        if (a.Length == 0 || b.Length == 0) return 0;
        if (a == b) return 1;
        var aTokens = a.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        var bTokens = b.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        var contained = aTokens.IsSubsetOf(bTokens) || bTokens.IsSubsetOf(aTokens) ? 0.88 : 0;
        var distance = Levenshtein(a, b);
        var edit = 1d - (double)distance / Math.Max(a.Length, b.Length);
        return Math.Max(contained, edit);
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var lastWasSpace = true;
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                lastWasSpace = false;
            }
            else if (!lastWasSpace)
            {
                builder.Append(' ');
                lastWasSpace = true;
            }
        }
        return builder.ToString().TrimEnd();
    }

    private static int Levenshtein(string left, string right)
    {
        var previous = Enumerable.Range(0, right.Length + 1).ToArray();
        var current = new int[right.Length + 1];
        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + (left[i - 1] == right[j - 1] ? 0 : 1));
            (previous, current) = (current, previous);
        }
        return previous[right.Length];
    }
}
