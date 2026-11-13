using System.Globalization;
using System.Text;

namespace CrestApps.OrchardCore.Numbering;

/// <summary>
/// How the numbers of a series are written, such as <c>INV-{yyyy}-{seq:0000}</c>, where they start, and when they start
/// again.
/// </summary>
/// <remarks>
/// A pattern is plain text with tokens in braces: <c>{seq}</c> is the number, <c>{seq:0000}</c> the number written with
/// at least that many digits, <c>{yyyy}</c> and <c>{yy}</c> the year, and <c>{MM}</c> the month of the date the number
/// is issued on.
/// </remarks>
public sealed class NumberSeriesFormat
{
    /// <summary>
    /// The longest pattern a format may have.
    /// </summary>
    public const int MaximumPatternLength = 40;

    private const string SequenceToken = "seq";
    private const string LongYearToken = "yyyy";
    private const string ShortYearToken = "yy";
    private const string MonthToken = "MM";

    /// <summary>
    /// Initializes a new instance of the <see cref="NumberSeriesFormat"/> class that writes the bare number.
    /// </summary>
    public NumberSeriesFormat()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NumberSeriesFormat"/> class.
    /// </summary>
    /// <param name="pattern">The pattern, such as <c>INV-{seq:0000}</c>.</param>
    /// <param name="firstNumber">The first number of the series.</param>
    /// <param name="reset">When the numbers start again.</param>
    public NumberSeriesFormat(string pattern, long firstNumber = 1, NumberSeriesReset reset = NumberSeriesReset.Never)
    {
        Pattern = pattern;
        FirstNumber = firstNumber;
        Reset = reset;
    }

    /// <summary>
    /// Gets or sets the pattern the numbers are written with.
    /// </summary>
    public string Pattern { get; set; } = "{seq}";

    /// <summary>
    /// Gets or sets the first number of the series, and of each new year or month when the numbers start again.
    /// </summary>
    public long FirstNumber { get; set; } = 1;

    /// <summary>
    /// Gets or sets when the numbers start again.
    /// </summary>
    public NumberSeriesReset Reset { get; set; }

    /// <summary>
    /// Writes a number of the series.
    /// </summary>
    /// <param name="sequence">The number in the series.</param>
    /// <param name="date">The date the number is issued on, in the site's time zone.</param>
    /// <returns>The number as the pattern writes it. A pattern that cannot be used writes the bare number.</returns>
    public string Format(long sequence, DateOnly date)
    {
        if (!TryParse(Pattern, out var parts))
        {
            return sequence.ToString(CultureInfo.InvariantCulture);
        }

        var builder = new StringBuilder();

        foreach (var part in parts)
        {
            switch (part.Token)
            {
                case null:
                    builder.Append(part.Text);
                    break;
                case SequenceToken:
                    builder.Append(sequence.ToString("D" + Math.Max(1, part.Digits).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture));
                    break;
                case LongYearToken:
                    builder.Append(date.Year.ToString("D4", CultureInfo.InvariantCulture));
                    break;
                case ShortYearToken:
                    builder.Append((date.Year % 100).ToString("D2", CultureInfo.InvariantCulture));
                    break;
                case MonthToken:
                    builder.Append(date.Month.ToString("D2", CultureInfo.InvariantCulture));
                    break;
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Gets the period a date falls in, which has its own numbers when the series starts again each year or month.
    /// </summary>
    /// <param name="date">The date the number is issued on, in the site's time zone.</param>
    /// <returns>The period, such as <c>2026</c> or <c>2026-11</c>, or <see langword="null"/> when the numbers never
    /// start again.</returns>
    public string GetPeriod(DateOnly date)
        => Reset switch
        {
            NumberSeriesReset.Yearly => date.Year.ToString("D4", CultureInfo.InvariantCulture),
            NumberSeriesReset.Monthly => date.Year.ToString("D4", CultureInfo.InvariantCulture) + "-" + date.Month.ToString("D2", CultureInfo.InvariantCulture),
            _ => null,
        };

    /// <summary>
    /// Checks that the format can be used: the pattern has exactly one number token and only known tokens, and a series
    /// that starts again has the date tokens that keep its numbers apart.
    /// </summary>
    /// <returns>Why the format cannot be used, or <see cref="NumberSeriesFormatError.None"/> when it can.</returns>
    public NumberSeriesFormatError Validate()
    {
        if (string.IsNullOrWhiteSpace(Pattern))
        {
            return NumberSeriesFormatError.Empty;
        }

        if (Pattern.Length > MaximumPatternLength)
        {
            return NumberSeriesFormatError.TooLong;
        }

        if (!TryParse(Pattern, out var parts))
        {
            return NumberSeriesFormatError.UnknownToken;
        }

        var sequences = parts.Count(part => part.Token == SequenceToken);

        if (sequences == 0)
        {
            return NumberSeriesFormatError.MissingSequence;
        }

        if (sequences > 1)
        {
            return NumberSeriesFormatError.SeveralSequences;
        }

        if (FirstNumber < 1)
        {
            return NumberSeriesFormatError.FirstNumberTooLow;
        }

        if (Reset != NumberSeriesReset.Never && !parts.Any(part => part.Token is LongYearToken or ShortYearToken))
        {
            return NumberSeriesFormatError.ResetNeedsYear;
        }

        if (Reset == NumberSeriesReset.Monthly && !parts.Any(part => part.Token == MonthToken))
        {
            return NumberSeriesFormatError.ResetNeedsMonth;
        }

        return NumberSeriesFormatError.None;
    }

    private static bool TryParse(string pattern, out List<PatternPart> parts)
    {
        parts = [];

        if (string.IsNullOrEmpty(pattern))
        {
            return false;
        }

        var position = 0;

        while (position < pattern.Length)
        {
            var open = pattern.IndexOf('{', position);
            var text = open < 0 ? pattern[position..] : pattern[position..open];

            // A closing brace outside a token is a pattern typed wrong, not text.
            if (text.Contains('}'))
            {
                return false;
            }

            if (text.Length > 0)
            {
                parts.Add(new PatternPart(text, null, 0));
            }

            if (open < 0)
            {
                break;
            }

            var close = pattern.IndexOf('}', open);

            if (close < 0)
            {
                return false;
            }

            var token = pattern[(open + 1)..close];

            if (token is LongYearToken or ShortYearToken or MonthToken or SequenceToken)
            {
                parts.Add(new PatternPart(null, token, 1));
            }
            else if (token.StartsWith(SequenceToken + ":", StringComparison.Ordinal) &&
                token.Length > SequenceToken.Length + 1 &&
                token.AsSpan(SequenceToken.Length + 1).IndexOfAnyExcept('0') < 0)
            {
                parts.Add(new PatternPart(null, SequenceToken, token.Length - SequenceToken.Length - 1));
            }
            else
            {
                return false;
            }

            position = close + 1;
        }

        return true;
    }

    private readonly record struct PatternPart(string Text, string Token, int Digits);
}
