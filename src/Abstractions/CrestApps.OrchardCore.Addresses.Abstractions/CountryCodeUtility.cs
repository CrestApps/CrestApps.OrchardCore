namespace CrestApps.OrchardCore.Addresses;

/// <summary>
/// Normalizes and checks ISO 3166-1 alpha-2 country codes, such as US or DE.
/// </summary>
public static class CountryCodeUtility
{
    /// <summary>
    /// Trims a country code and writes it in upper case.
    /// </summary>
    /// <param name="countryCode">The country code as entered.</param>
    /// <returns>The normalized code, or <see langword="null"/> when none was entered.</returns>
    public static string Normalize(string countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
        {
            return null;
        }

        return countryCode.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// Whether a country code, once normalized, is two ASCII letters. It checks the form of the code, not that the
    /// country exists.
    /// </summary>
    /// <param name="countryCode">The country code as entered.</param>
    public static bool IsCountryCode(string countryCode)
    {
        var normalizedCode = Normalize(countryCode);

        return normalizedCode is { Length: 2 } &&
            char.IsAsciiLetter(normalizedCode[0]) &&
            char.IsAsciiLetter(normalizedCode[1]);
    }
}
