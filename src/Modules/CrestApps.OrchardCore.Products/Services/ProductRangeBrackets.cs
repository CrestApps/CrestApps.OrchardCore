using System.Globalization;
using CrestApps.OrchardCore.Products.Core.Listing;

namespace CrestApps.OrchardCore.Products.Services;

/// <summary>
/// Reads and writes the ready-made ranges of a range filter as a merchant types them: <c>-25, 25-75, 75+</c>
/// for up to 25, 25 to 75, and 75 and up.
/// </summary>
/// <remarks>
/// A leading dash means "up to", so negative values cannot be typed. Numbers use a dot for decimals and no
/// thousands separators, since a comma separates ranges.
/// </remarks>
public static class ProductRangeBrackets
{
    private static readonly char[] _separators = [',', ';', '\r', '\n'];

    /// <summary>
    /// Reads typed ranges. Returns <see langword="false"/>, with the first range that could not be read, when
    /// one is not a number, a range, or an open-ended range, or when its lowest value is not below its highest.
    /// </summary>
    /// <param name="text">The ranges as typed.</param>
    /// <param name="ranges">The ranges read, in the order typed.</param>
    /// <param name="invalid">The first range that could not be read.</param>
    public static bool TryParse(string text, out List<ProductFacetRange> ranges, out string invalid)
    {
        ranges = [];
        invalid = null;

        foreach (var entry in (text ?? string.Empty).Split(_separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var range = ParseOne(entry);

            if (range is null)
            {
                invalid = entry;
                ranges = [];

                return false;
            }

            ranges.Add(range);
        }

        return true;
    }

    /// <summary>
    /// Writes ranges the way they are typed.
    /// </summary>
    /// <param name="ranges">The ranges.</param>
    public static string Format(IEnumerable<ProductFacetRange> ranges)
        => string.Join(", ", (ranges ?? []).Select(range => (range.Min, range.Max) switch
        {
            (null, { } max) => "-" + Invariant(max),
            ({ } min, null) => Invariant(min) + "+",
            ({ } min, { } max) => Invariant(min) + "-" + Invariant(max),
            _ => string.Empty,
        }).Where(entry => entry.Length > 0));

    private static ProductFacetRange ParseOne(string entry)
    {
        if (entry.EndsWith('+'))
        {
            return TryNumber(entry[..^1], out var min) ? new ProductFacetRange { Min = min } : null;
        }

        if (entry.StartsWith('-'))
        {
            return TryNumber(entry[1..], out var max) ? new ProductFacetRange { Max = max } : null;
        }

        var dash = entry.IndexOf('-');

        if (dash <= 0 ||
            !TryNumber(entry[..dash], out var low) ||
            !TryNumber(entry[(dash + 1)..], out var high) ||
            low >= high)
        {
            return null;
        }

        return new ProductFacetRange { Min = low, Max = high };
    }

    private static bool TryNumber(string text, out decimal value)
        => decimal.TryParse(text.Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);

    private static string Invariant(decimal value)
        => value.ToString("0.##", CultureInfo.InvariantCulture);
}
