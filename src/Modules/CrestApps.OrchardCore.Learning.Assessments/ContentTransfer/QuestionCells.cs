using System.Text;

namespace CrestApps.OrchardCore.Learning.Assessments.ContentTransfer;

/// <summary>
/// The text of the list cells of the question columns, such as <c>Foam|*CO2|Water</c>. Values are separated by
/// <c>|</c>; a backslash before one of <c>| * = \</c> keeps that character as text, so a value may hold <c>\|</c>, <c>\\</c>, a leading
/// <c>\*</c> (an option that starts with a star but is not marked right) or, in a pair, <c>\=</c>.
/// </summary>
public static class QuestionCells
{
    /// <summary>
    /// The separator of the values of a list cell.
    /// </summary>
    public const char Separator = '|';

    /// <summary>
    /// The mark of a right option.
    /// </summary>
    public const char RightMark = '*';

    /// <summary>
    /// The separator of the prompt and the answer of a pair.
    /// </summary>
    public const char PairSeparator = '=';

    private const char Escape = '\\';

    /// <summary>
    /// Writes values as a list cell.
    /// </summary>
    /// <param name="values">The values, already escaped with <see cref="EscapeValue"/>.</param>
    public static string Join(IEnumerable<string> values)
        => string.Join(Separator, values);

    /// <summary>
    /// Escapes a value of a list cell.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="pair">Whether the value is a side of a pair, where <c>=</c> separates the sides.</param>
    public static string EscapeValue(string value, bool pair = false)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length + 4);

        for (var i = 0; i < value.Length; i++)
        {
            var character = value[i];

            if (character is Escape or Separator || (pair && character == PairSeparator) || (i == 0 && character == RightMark))
            {
                builder.Append(Escape);
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Splits a list cell into its values, still escaped, at each separator that is not escaped.
    /// </summary>
    /// <param name="cell">The cell.</param>
    /// <param name="keepEmpty">Whether empty values are kept, for a list that lines up with another.</param>
    public static List<string> SplitRaw(string cell, bool keepEmpty = false)
    {
        var values = new List<string>();

        if (string.IsNullOrEmpty(cell))
        {
            return values;
        }

        var builder = new StringBuilder();

        for (var i = 0; i < cell.Length; i++)
        {
            var character = cell[i];

            if (character == Escape && i + 1 < cell.Length && IsEscaped(cell[i + 1]))
            {
                builder.Append(character).Append(cell[++i]);

                continue;
            }

            if (character == Separator)
            {
                Add(values, builder.ToString(), keepEmpty);
                builder.Clear();

                continue;
            }

            builder.Append(character);
        }

        Add(values, builder.ToString(), keepEmpty);

        return values;
    }

    /// <summary>
    /// Splits a list cell into its values, unescaped.
    /// </summary>
    /// <param name="cell">The cell.</param>
    /// <param name="keepEmpty">Whether empty values are kept.</param>
    public static List<string> Split(string cell, bool keepEmpty = false)
        => SplitRaw(cell, keepEmpty).Select(Unescape).ToList();

    /// <summary>
    /// Reads whether a raw value is marked right with a leading <c>*</c>, and the rest of it unescaped.
    /// </summary>
    /// <param name="raw">The raw value.</param>
    /// <param name="text">The value without its mark, unescaped.</param>
    public static bool IsMarkedRight(string raw, out string text)
    {
        var marked = raw.StartsWith(RightMark);
        text = Unescape(marked ? raw[1..].Trim() : raw);

        return marked;
    }

    /// <summary>
    /// Splits a raw value at its first <c>=</c> that is not escaped.
    /// </summary>
    /// <param name="raw">The raw value.</param>
    /// <param name="prompt">The prompt, unescaped.</param>
    /// <param name="answer">The answer, unescaped.</param>
    /// <returns>Whether the value is a pair.</returns>
    public static bool TrySplitPair(string raw, out string prompt, out string answer)
    {
        for (var i = 0; i < raw.Length; i++)
        {
            if (raw[i] == Escape && i + 1 < raw.Length && IsEscaped(raw[i + 1]))
            {
                i++;

                continue;
            }

            if (raw[i] == PairSeparator)
            {
                prompt = Unescape(raw[..i]).Trim();
                answer = Unescape(raw[(i + 1)..]).Trim();

                return prompt.Length > 0 && answer.Length > 0;
            }
        }

        prompt = null;
        answer = null;

        return false;
    }

    /// <summary>
    /// Removes the escapes of a value.
    /// </summary>
    /// <param name="raw">The raw value.</param>
    public static string Unescape(string raw)
    {
        if (string.IsNullOrEmpty(raw) || raw.IndexOf(Escape) < 0)
        {
            return raw?.Trim() ?? string.Empty;
        }

        var builder = new StringBuilder(raw.Length);

        for (var i = 0; i < raw.Length; i++)
        {
            if (raw[i] == Escape && i + 1 < raw.Length && IsEscaped(raw[i + 1]))
            {
                builder.Append(raw[++i]);

                continue;
            }

            builder.Append(raw[i]);
        }

        return builder.ToString().Trim();
    }

    // Only the characters the cells give a meaning to are escaped, so a backslash before any other character, such as
    // in a path, is kept as text.
    private static bool IsEscaped(char character)
        => character is Escape or Separator or RightMark or PairSeparator;

    private static void Add(List<string> values, string value, bool keepEmpty)
    {
        var trimmed = value.Trim();

        if (keepEmpty || trimmed.Length > 0)
        {
            values.Add(trimmed);
        }
    }
}
