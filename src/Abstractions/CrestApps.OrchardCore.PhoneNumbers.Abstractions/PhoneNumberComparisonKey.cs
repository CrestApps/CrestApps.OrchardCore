namespace CrestApps.OrchardCore.PhoneNumbers;

/// <summary>
/// Produces the keys used to decide whether two phone numbers written by different people, in different
/// formats, refer to the same line.
/// <para>
/// This is deliberately not canonicalization. <see cref="PhoneNumber"/> answers "what is this number", and a
/// value that cannot be canonicalized has no answer. Matching a spreadsheet against contacts that were typed
/// in over years is a different question: the stored value may never have been canonical, so the comparison
/// has to fall back to something looser. Keeping that fallback here, named for what it is, is what stops it
/// from being reinvented at each call site and then mistaken for canonicalization the way it once was on the
/// compliance path.
/// </para>
/// </summary>
public static class PhoneNumberComparisonKey
{
    /// <summary>
    /// Returns the single key that identifies a number for comparison against other numbers keyed the same way.
    /// </summary>
    /// <param name="canonical">The canonical form of the number, or the default value when it could not be canonicalized.</param>
    /// <param name="rawValue">The number exactly as it was written.</param>
    /// <returns>The canonical value when there is one, otherwise the digits of the raw value, otherwise an empty string.</returns>
    public static string For(PhoneNumber canonical, string rawValue)
    {
        if (canonical.HasValue)
        {
            return canonical.Value;
        }

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return string.Empty;
        }

        return DigitsOf(rawValue);
    }

    /// <summary>
    /// Returns every key a number may be matched by, so a value stored in one shape still matches the same
    /// line written in another.
    /// </summary>
    /// <param name="canonical">The canonical form of the number, or the default value when it could not be canonicalized.</param>
    /// <param name="rawValue">The number exactly as it was written.</param>
    /// <returns>The distinct, case-insensitive set of comparison keys, which is empty when there is nothing to compare.</returns>
    public static string[] AllFor(PhoneNumber canonical, string rawValue)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (canonical.HasValue)
        {
            keys.Add(canonical.Value);
        }

        if (!string.IsNullOrWhiteSpace(rawValue))
        {
            var digits = DigitsOf(rawValue);

            if (digits.Length > 0)
            {
                keys.Add(digits);
            }

            var trimmedValue = rawValue.Trim();

            if (trimmedValue.Length > 0)
            {
                keys.Add(trimmedValue);
            }
        }

        return [.. keys];
    }

    private static string DigitsOf(string value)
        => new(value.Where(char.IsDigit).ToArray());
}
