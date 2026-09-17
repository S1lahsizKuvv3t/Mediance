using System.Globalization;
using System.Text;

namespace Mediance.Core.Lyrics;

public sealed record TimedSpeechSegment(TimeSpan Start, TimeSpan End, string Text);

public sealed record AutomaticLyricsAlignment(
    IReadOnlyList<TimeSpan> LineStarts,
    double Confidence,
    int AnchoredLines)
{
    public bool IsReliable(int lineCount) =>
        lineCount > 0 && LineStarts.Count == lineCount && Confidence >= 0.58 &&
        AnchoredLines >= Math.Max(3, (int)Math.Ceiling(lineCount * 0.45));
}

public enum AutomaticLyricsSyncStage { Capturing, DownloadingModel, Transcribing, Aligning }

public sealed record AutomaticLyricsSyncProgress(AutomaticLyricsSyncStage Stage, double? Fraction = null);

public sealed record AutomaticLyricsSyncRequest(
    string SourceAppId,
    LyricsQuery Query,
    string PlainText,
    TimeSpan CaptureOffset);

public interface IAutomaticLyricsSynchronizer
{
    event EventHandler<AutomaticLyricsSyncProgress>? ProgressChanged;
    Task<AutomaticLyricsAlignment?> SynchronizeAsync(
        AutomaticLyricsSyncRequest request, CancellationToken token = default);
}

public static class AutomaticLyricsAligner
{
    private const double GapPenalty = -0.46;

    public static AutomaticLyricsAlignment Align(
        string plainText,
        IReadOnlyList<TimedSpeechSegment> transcript,
        TimeSpan captureOffset,
        TimeSpan? trackDuration = null)
    {
        var lines = PlainLyricsTimeline.Clean(plainText).Split('\n',
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0 || transcript.Count == 0)
            return new([], 0, 0);

        var lyricTokens = TokenizeLines(lines);
        var speechTokens = TokenizeTranscript(transcript, captureOffset);
        if (lyricTokens.Count == 0 || speechTokens.Count == 0)
            return new([], 0, 0);

        var matches = AlignTokens(lyricTokens, speechTokens);
        var anchors = new TimeSpan?[lines.Length];
        foreach (var match in matches)
        {
            var lyric = lyricTokens[match.LyricIndex];
            var speech = speechTokens[match.SpeechIndex];
            if (anchors[lyric.LineIndex] is null || speech.Start < anchors[lyric.LineIndex])
                anchors[lyric.LineIndex] = speech.Start;
        }

        var anchoredLines = anchors.Count(value => value is not null);
        if (anchoredLines == 0) return new([], 0, 0);

        var starts = FillMissingStarts(lines, anchors, lyricTokens, captureOffset, trackDuration);
        var matchedWeight = matches.Sum(match => match.Score);
        var tokenCoverage = matchedWeight / Math.Max(1, lyricTokens.Count);
        var lineCoverage = (double)anchoredLines / lines.Length;
        var ordered = StartsAreStrictlyIncreasing(starts);
        var confidence = Math.Clamp(tokenCoverage * 0.72 + lineCoverage * 0.28, 0, 1);
        if (!ordered) confidence *= 0.35;
        return new(starts, confidence, anchoredLines);
    }

    private static List<LyricToken> TokenizeLines(IReadOnlyList<string> lines)
    {
        var result = new List<LyricToken>();
        for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
            foreach (var token in Tokenize(lines[lineIndex]))
                result.Add(new(token, lineIndex));
        return result;
    }

    private static List<SpeechToken> TokenizeTranscript(
        IReadOnlyList<TimedSpeechSegment> transcript, TimeSpan captureOffset)
    {
        var result = new List<SpeechToken>();
        foreach (var segment in transcript.OrderBy(value => value.Start))
        {
            var tokens = Tokenize(segment.Text);
            if (tokens.Count == 0) continue;
            var duration = segment.End > segment.Start ? segment.End - segment.Start : TimeSpan.FromMilliseconds(250);
            for (var index = 0; index < tokens.Count; index++)
            {
                var fraction = (double)index / tokens.Count;
                var timestamp = captureOffset + segment.Start + TimeSpan.FromTicks((long)(duration.Ticks * fraction));
                result.Add(new(tokens[index], timestamp));
            }
        }
        return result;
    }

    private static List<TokenMatch> AlignTokens(
        IReadOnlyList<LyricToken> lyrics, IReadOnlyList<SpeechToken> speech)
    {
        var rows = lyrics.Count + 1;
        var columns = speech.Count + 1;
        var scores = new double[rows, columns];
        var moves = new byte[rows, columns];
        for (var row = 1; row < rows; row++) scores[row, 0] = row * GapPenalty;
        for (var column = 1; column < columns; column++) scores[0, column] = column * GapPenalty;

        for (var row = 1; row < rows; row++)
        {
            for (var column = 1; column < columns; column++)
            {
                var similarity = TokenSimilarity(lyrics[row - 1].Value, speech[column - 1].Value);
                var diagonal = scores[row - 1, column - 1] + (similarity >= 0.58 ? similarity : -0.72);
                var up = scores[row - 1, column] + GapPenalty;
                var left = scores[row, column - 1] + GapPenalty;
                if (diagonal >= up && diagonal >= left)
                {
                    scores[row, column] = diagonal;
                    moves[row, column] = 1;
                }
                else if (up >= left)
                {
                    scores[row, column] = up;
                    moves[row, column] = 2;
                }
                else
                {
                    scores[row, column] = left;
                    moves[row, column] = 3;
                }
            }
        }

        var matches = new List<TokenMatch>();
        var lyricIndex = lyrics.Count;
        var speechIndex = speech.Count;
        while (lyricIndex > 0 || speechIndex > 0)
        {
            var move = moves[lyricIndex, speechIndex];
            if (move == 1)
            {
                var similarity = TokenSimilarity(lyrics[lyricIndex - 1].Value, speech[speechIndex - 1].Value);
                if (similarity >= 0.58)
                    matches.Add(new(lyricIndex - 1, speechIndex - 1, similarity));
                lyricIndex--;
                speechIndex--;
            }
            else if (move == 2 || speechIndex == 0) lyricIndex--;
            else speechIndex--;
        }
        matches.Reverse();
        return matches;
    }

    private static TimeSpan[] FillMissingStarts(
        IReadOnlyList<string> lines,
        IReadOnlyList<TimeSpan?> anchors,
        IReadOnlyList<LyricToken> tokens,
        TimeSpan captureOffset,
        TimeSpan? trackDuration)
    {
        var weights = Enumerable.Range(0, lines.Count)
            .Select(index => Math.Max(1, tokens.Count(token => token.LineIndex == index))).ToArray();
        var result = new TimeSpan[lines.Count];
        var anchored = Enumerable.Range(0, anchors.Count).Where(index => anchors[index] is not null).ToArray();

        for (var anchorIndex = 0; anchorIndex < anchored.Length; anchorIndex++)
        {
            var line = anchored[anchorIndex];
            result[line] = anchors[line]!.Value;
            var next = anchorIndex + 1 < anchored.Length ? anchored[anchorIndex + 1] : -1;
            if (next < 0) continue;
            Interpolate(result, weights, line, next, result[line], anchors[next]!.Value);
        }

        var first = anchored[0];
        if (first > 0)
        {
            var available = anchors[first]!.Value - captureOffset;
            var totalWeight = weights.Take(first + 1).Sum();
            var cursor = captureOffset;
            for (var index = 0; index < first; index++)
            {
                result[index] = cursor;
                cursor += Scale(available, (double)weights[index] / totalWeight);
            }
        }

        var last = anchored[^1];
        if (last < lines.Count - 1)
        {
            var end = trackDuration is { } duration && duration > result[last]
                ? duration
                : result[last] + TimeSpan.FromSeconds((lines.Count - last) * 3.2);
            Interpolate(result, weights, last, lines.Count, result[last], end);
        }

        for (var index = 1; index < result.Length; index++)
            if (result[index] <= result[index - 1])
                result[index] = result[index - 1] + TimeSpan.FromMilliseconds(50);
        return result;
    }

    private static void Interpolate(TimeSpan[] result, IReadOnlyList<int> weights,
        int fromLine, int exclusiveToLine, TimeSpan fromTime, TimeSpan toTime)
    {
        var totalWeight = weights.Skip(fromLine).Take(exclusiveToLine - fromLine).Sum();
        var cursor = fromTime;
        for (var index = fromLine + 1; index < exclusiveToLine; index++)
        {
            cursor += Scale(toTime - fromTime, (double)weights[index - 1] / totalWeight);
            result[index] = cursor;
        }
    }

    private static TimeSpan Scale(TimeSpan value, double factor) =>
        TimeSpan.FromTicks((long)(value.Ticks * factor));

    private static bool StartsAreStrictlyIncreasing(IReadOnlyList<TimeSpan> starts)
    {
        for (var index = 1; index < starts.Count; index++)
            if (starts[index] <= starts[index - 1]) return false;
        return starts.Count > 0 && starts[0] >= TimeSpan.Zero;
    }

    private static IReadOnlyList<string> Tokenize(string value) =>
        Normalize(value).Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private static string Normalize(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var previousSpace = true;
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                previousSpace = false;
            }
            else if (!previousSpace)
            {
                builder.Append(' ');
                previousSpace = true;
            }
        }
        return builder.ToString().Trim();
    }

    private static double TokenSimilarity(string left, string right)
    {
        if (left == right) return 1;
        if (left.Length >= 5 && right.Length >= 5 &&
            (left.StartsWith(right, StringComparison.Ordinal) || right.StartsWith(left, StringComparison.Ordinal)))
            return 0.82;
        var maximum = Math.Max(left.Length, right.Length);
        if (maximum == 0) return 1;
        var similarity = 1d - (double)Levenshtein(left, right) / maximum;
        return similarity;
    }

    private static int Levenshtein(string left, string right)
    {
        var previous = Enumerable.Range(0, right.Length + 1).ToArray();
        var current = new int[right.Length + 1];
        for (var leftIndex = 1; leftIndex <= left.Length; leftIndex++)
        {
            current[0] = leftIndex;
            for (var rightIndex = 1; rightIndex <= right.Length; rightIndex++)
                current[rightIndex] = Math.Min(
                    Math.Min(current[rightIndex - 1] + 1, previous[rightIndex] + 1),
                    previous[rightIndex - 1] + (left[leftIndex - 1] == right[rightIndex - 1] ? 0 : 1));
            (previous, current) = (current, previous);
        }
        return previous[right.Length];
    }

    private sealed record LyricToken(string Value, int LineIndex);
    private sealed record SpeechToken(string Value, TimeSpan Start);
    private sealed record TokenMatch(int LyricIndex, int SpeechIndex, double Score);
}
