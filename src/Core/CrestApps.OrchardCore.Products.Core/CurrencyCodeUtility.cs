namespace CrestApps.OrchardCore.Products.Core;

/// <summary>
/// Normalizes and validates ISO 4217 currency codes.
/// </summary>
public static class CurrencyCodeUtility
{
    /// <summary>
    /// Trims a currency code and writes it in upper case.
    /// </summary>
    /// <param name="currencyCode">The currency code as entered.</param>
    /// <returns>The normalized code, or <see langword="null"/> when none was entered.</returns>
    public static string Normalize(string currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode))
        {
            return null;
        }

        return currencyCode.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// Whether a currency code, once normalized, is three ASCII letters.
    /// </summary>
    /// <param name="currencyCode">The currency code as entered.</param>
    public static bool IsValid(string currencyCode)
    {
        var normalizedCode = Normalize(currencyCode);

        return normalizedCode is { Length: 3 } &&
            normalizedCode.All(char.IsAsciiLetter);
    }
}
