using System.Globalization;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// A range of attempt numbers written the way an operator types it: <c>4</c> for exactly four, <c>4+</c> for four or
/// more, <c>4-</c> for four or fewer, and <c>2..5</c> for two to five. Either end of <c>..</c> can be left off, so
/// <c>4..</c> means four or more and <c>..4</c> four or fewer.
/// </summary>
/// <param name="Minimum">The lowest attempt number included, or <see langword="null"/> for no lower bound.</param>
/// <param name="Maximum">The highest attempt number included, or <see langword="null"/> for no upper bound.</param>
public readonly record struct ActivityAttemptRange(int? Minimum, int? Maximum)
{
    /// <summary>
    /// Reads an attempt range.
    /// </summary>
    /// <param name="value">The text to read.</param>
    /// <param name="range">The range read, when the text is one.</param>
    /// <returns><see langword="true"/> when the text is a range; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(string value, out ActivityAttemptRange range)
    {
        range = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Replace(" ", string.Empty, StringComparison.Ordinal);

        var separator = text.IndexOf("..", StringComparison.Ordinal);

        if (separator >= 0)
        {
            var lower = text[..separator];
            var upper = text[(separator + 2)..];

            if (lower.Length == 0 && upper.Length == 0)
            {
                return false;
            }

            int? minimum = null;
            int? maximum = null;

            if (lower.Length > 0)
            {
                if (!TryReadCount(lower, out var parsedMinimum))
                {
                    return false;
                }

                minimum = parsedMinimum;
            }

            if (upper.Length > 0)
            {
                if (!TryReadCount(upper, out var parsedMaximum))
                {
                    return false;
                }

                maximum = parsedMaximum;
            }

            if (minimum > maximum)
            {
                return false;
            }

            range = new ActivityAttemptRange(minimum, maximum);

            return true;
        }

        if (text.EndsWith('+'))
        {
            if (!TryReadCount(text[..^1], out var atLeast))
            {
                return false;
            }

            range = new ActivityAttemptRange(atLeast, null);

            return true;
        }

        if (text.EndsWith('-'))
        {
            if (!TryReadCount(text[..^1], out var atMost))
            {
                return false;
            }

            range = new ActivityAttemptRange(null, atMost);

            return true;
        }

        if (!TryReadCount(text, out var exactly))
        {
            return false;
        }

        range = new ActivityAttemptRange(exactly, exactly);

        return true;
    }

    /// <summary>
    /// Writes the range back the way it is typed.
    /// </summary>
    public override string ToString()
        => (Minimum, Maximum) switch
        {
            ({ } minimum, { } maximum) when minimum == maximum => minimum.ToString(CultureInfo.InvariantCulture),
            ({ } minimum, { } maximum) => $"{minimum.ToString(CultureInfo.InvariantCulture)}..{maximum.ToString(CultureInfo.InvariantCulture)}",
            ({ } minimum, null) => $"{minimum.ToString(CultureInfo.InvariantCulture)}+",
            (null, { } maximum) => $"{maximum.ToString(CultureInfo.InvariantCulture)}-",
            _ => string.Empty,
        };

    // An attempt count is a whole number of at least zero; a minus sign is read as "or fewer", never as negative.
    private static bool TryReadCount(string text, out int count)
        => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out count);
}
