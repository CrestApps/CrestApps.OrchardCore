namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// Blanks the comments of C# source, and optionally its string and character literals, so a text scan sees only code.
/// </summary>
/// <remarks>
/// Every blanked character becomes a space and line breaks are kept, so positions and line numbers in the masked text
/// are those of the source. A brace or a pattern inside a comment or a string no longer misleads a scan that counts
/// braces or matches a call over several lines. The literal's quotes are kept, so <c>S["Text", arg]</c> still reads as
/// an indexer with a string argument.
/// </remarks>
internal static class CSharpSourceMask
{
    /// <summary>
    /// Returns the source with comments blanked.
    /// </summary>
    public static string Comments(string source)
        => Mask(source, maskLiterals: false);

    /// <summary>
    /// Returns the source with comments and the contents of string and character literals blanked.
    /// </summary>
    public static string CommentsAndLiterals(string source)
        => Mask(source, maskLiterals: true);

    /// <summary>
    /// Finds the bracket that closes the one at <paramref name="open"/>, in text whose comments and literals are blanked.
    /// </summary>
    /// <returns>The index of the closing bracket, or the end of the text when it is never closed.</returns>
    public static int FindClosing(string code, int open)
    {
        var opening = code[open];
        var closing = opening switch
        {
            '(' => ')',
            '{' => '}',
            '[' => ']',
            '<' => '>',
            _ => throw new ArgumentException($"'{opening}' is not an opening bracket.", nameof(open)),
        };
        var depth = 0;

        for (var index = open; index < code.Length; index++)
        {
            if (code[index] == opening)
            {
                depth++;
            }
            else if (code[index] == closing && --depth == 0)
            {
                return index;
            }
        }

        return code.Length;
    }

    /// <summary>
    /// Counts the parameters of a parameter list, given the text between its parentheses.
    /// </summary>
    public static int CountParameters(string parameters)
    {
        if (string.IsNullOrWhiteSpace(parameters))
        {
            return 0;
        }

        var count = 1;
        var depth = 0;

        foreach (var character in parameters)
        {
            if (character is '(' or '<' or '[' or '{')
            {
                depth++;
            }
            else if (character is ')' or '>' or ']' or '}')
            {
                depth--;
            }
            else if (character == ',' && depth == 0)
            {
                count++;
            }
        }

        return count;
    }

    private static string Mask(string source, bool maskLiterals)
    {
        var output = source.ToCharArray();
        var length = source.Length;
        var index = 0;

        while (index < length)
        {
            var current = source[index];
            var next = index + 1 < length ? source[index + 1] : '\0';

            if (current == '/' && next == '/')
            {
                var end = source.IndexOf('\n', index);
                end = end < 0 ? length : end;
                Blank(output, index, end);
                index = end;
            }
            else if (current == '/' && next == '*')
            {
                var end = source.IndexOf("*/", index + 2, StringComparison.Ordinal);
                end = end < 0 ? length : end + 2;
                Blank(output, index, end);
                index = end;
            }
            else if (current == '\'')
            {
                var end = SkipCharacter(source, index);
                BlankLiteral(output, index + 1, end - 1, maskLiterals);
                index = end;
            }
            else if (current is '"' or '$' or '@' && TryReadString(source, index, out var contentStart, out var contentEnd, out var end))
            {
                BlankLiteral(output, contentStart, contentEnd, maskLiterals);
                index = end;
            }
            else
            {
                index++;
            }
        }

        return new string(output);
    }

    /// <summary>
    /// Reads a string literal of any kind starting at <paramref name="start"/>: regular, verbatim, interpolated or raw.
    /// </summary>
    private static bool TryReadString(string source, int start, out int contentStart, out int contentEnd, out int end)
    {
        var index = start;
        var verbatim = false;
        var interpolated = 0;

        while (index < source.Length && (source[index] == '$' || source[index] == '@'))
        {
            if (source[index] == '@')
            {
                verbatim = true;
            }
            else
            {
                interpolated++;
            }

            index++;
        }

        contentStart = contentEnd = end = start;

        if (index >= source.Length || source[index] != '"' || index - start > 4)
        {
            return false;
        }

        // An identifier such as @event or a $ outside a string is not a literal.
        var quotes = 0;

        while (index + quotes < source.Length && source[index + quotes] == '"')
        {
            quotes++;
        }

        if (quotes >= 3)
        {
            // A raw string ends at the first run of as many quotes as it opened with.
            var delimiter = new string('"', quotes);
            var close = source.IndexOf(delimiter, index + quotes, StringComparison.Ordinal);

            contentStart = index + quotes;
            contentEnd = close < 0 ? source.Length : close;
            end = close < 0 ? source.Length : close + quotes;

            while (end < source.Length && source[end] == '"')
            {
                end++;
            }

            return true;
        }

        contentStart = index + 1;
        var position = contentStart;
        var depth = 0;

        while (position < source.Length)
        {
            var character = source[position];

            if (interpolated > 0 && character == '{')
            {
                if (position + 1 < source.Length && source[position + 1] == '{' && depth == 0)
                {
                    position += 2;

                    continue;
                }

                depth++;
            }
            else if (interpolated > 0 && character == '}' && depth > 0)
            {
                depth--;
            }
            else if (depth > 0 && character == '"')
            {
                // A string nested in an interpolation hole.
                if (TryReadString(source, position, out _, out _, out var nestedEnd))
                {
                    position = nestedEnd;

                    continue;
                }
            }
            else if (depth == 0 && verbatim && character == '"')
            {
                if (position + 1 < source.Length && source[position + 1] == '"')
                {
                    position += 2;

                    continue;
                }

                break;
            }
            else if (depth == 0 && !verbatim && character == '\\')
            {
                position += 2;

                continue;
            }
            else if (depth == 0 && !verbatim && (character == '"' || character == '\n'))
            {
                break;
            }

            position++;
        }

        contentEnd = Math.Min(position, source.Length);
        end = Math.Min(position + 1, source.Length);

        return true;
    }

    private static int SkipCharacter(string source, int start)
    {
        var position = start + 1;

        while (position < source.Length && source[position] != '\'' && source[position] != '\n')
        {
            position += source[position] == '\\' ? 2 : 1;
        }

        return Math.Min(position + 1, source.Length);
    }

    private static void BlankLiteral(char[] output, int start, int end, bool maskLiterals)
    {
        if (maskLiterals)
        {
            Blank(output, start, end);
        }
    }

    private static void Blank(char[] output, int start, int end)
    {
        for (var index = start; index < end && index < output.Length; index++)
        {
            if (output[index] is not '\n' and not '\r')
            {
                output[index] = ' ';
            }
        }
    }
}
