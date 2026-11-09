using System.Globalization;

namespace CrestApps.OrchardCore.Numbering;

/// <summary>
/// Formats the numbers of an <see cref="INumberSequence"/>.
/// </summary>
public static class NumberSequenceExtensions
{
    /// <summary>
    /// Returns the next number of a series with a prefix, padded with zeros, such as <c>V-000042</c>.
    /// </summary>
    /// <param name="sequence">The sequence.</param>
    /// <param name="series">The name of the series.</param>
    /// <param name="prefix">The text before the number, such as <c>WO-</c>.</param>
    /// <param name="minimumDigits">The fewest digits the number is written with.</param>
    /// <param name="options">Where a new series starts.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The formatted number.</returns>
    public static async Task<string> NextAsync(
        this INumberSequence sequence,
        string series,
        string prefix,
        int minimumDigits,
        NumberSequenceOptions options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sequence);

        var number = await sequence.NextAsync(series, options, cancellationToken);

        return prefix + number.ToString("D" + Math.Max(1, minimumDigits).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Reads the highest number already used among values written as a prefix and digits, such as <c>WO-000042</c>, to
    /// seed a series with <see cref="NumberSequenceOptions.SeedAsync"/>.
    /// </summary>
    /// <param name="values">The numbers in use.</param>
    /// <param name="prefix">The prefix before the digits.</param>
    /// <returns>The highest number, or 0 when no value has the prefix and digits.</returns>
    public static long HighestNumber(IEnumerable<string> values, string prefix)
    {
        prefix ??= string.Empty;

        var highest = 0L;

        foreach (var value in values ?? [])
        {
            if (value is not null &&
                value.StartsWith(prefix, StringComparison.Ordinal) &&
                long.TryParse(value.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var number) &&
                number > highest)
            {
                highest = number;
            }
        }

        return highest;
    }
}
