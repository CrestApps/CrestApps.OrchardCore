namespace CrestApps.OrchardCore.Products.Services;

/// <summary>
/// Normalizes and validates ISO-4217 currency codes.
/// </summary>
internal static class CurrencyCodeUtility
{
    public static string Normalize(string currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode))
        {
            return null;
        }

        return currencyCode.Trim().ToUpperInvariant();
    }

    public static bool IsValid(string currencyCode)
    {
        var normalizedCode = Normalize(currencyCode);

        return normalizedCode is { Length: 3 } &&
            normalizedCode.All(static character => char.IsLetter(character));
    }
}
