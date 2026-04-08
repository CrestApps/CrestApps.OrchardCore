namespace CrestApps.Core.AI.Services;

public static class SentenceBoundaryDetector
{
    private static readonly HashSet<string> _abbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        "mr.",
        "mrs.",
        "ms.",
        "dr.",
        "prof.",
        "sr.",
        "jr.",
        "etc.",
        "vs.",
    };

    private const int SoftBoundaryMinLength = 120;
    private const int ForceFlushLength = 200;

    public static bool EndsWithSentenceBoundary(string text)
    {
        if (text is null || text.Length == 0)
        {
            return false;
        }

        return EndsWithSentenceBoundary(text.AsSpan());
    }

    public static bool EndsWithSentenceBoundary(ReadOnlySpan<char> span)
    {
        // Trim trailing spaces, tabs, and carriage returns but preserve
        // newlines since \n is a valid hard sentence boundary.
        span = span.TrimEnd(" \t\r");

        if (span.IsEmpty)
        {
            return false;
        }

        if (EndsWithHardBoundary(span))
        {
            if (EndsWithAbbreviation(span))
            {
                return false;
            }

            return true;
        }

        if (span.Length >= SoftBoundaryMinLength && EndsWithSoftBoundary(span))
        {
            return true;
        }

        if (span.Length >= ForceFlushLength)
        {
            return true;
        }

        return false;
    }

    private static bool EndsWithHardBoundary(ReadOnlySpan<char> span)
    {
        var i = span.Length - 1;

        while (i >= 0)
        {
            var c = span[i];

            if (IsTrailingWrapper(c))
            {
                i--;
                continue;
            }

            break;
        }

        if (i < 0)
        {
            return false;
        }

        return span[i] is '.' or '!' or '?' or '…' or '\n';
    }

    private static bool EndsWithSoftBoundary(ReadOnlySpan<char> span)
    {
        var last = span[^1];

        return last is ',' or ';' or ':' or '-';
    }

    private static bool IsTrailingWrapper(char c)
    {
        return c is '"' or '\'' or ')' or ']' or '}';
    }

    private static bool EndsWithAbbreviation(ReadOnlySpan<char> span)
    {
        var lastSpace = span.LastIndexOf(' ');
        var lastWord = lastSpace >= 0 ? span[(lastSpace + 1)..] : span;

        // fallback: convert lastWord to lowercase string to check abbreviation
        return _abbreviations.Contains(lastWord.ToString());
    }
}
