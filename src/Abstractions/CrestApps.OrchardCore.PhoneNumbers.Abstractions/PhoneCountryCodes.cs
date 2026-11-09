namespace CrestApps.OrchardCore.PhoneNumbers;

/// <summary>
/// Normalizes the country codes that come with phone numbers: a calling code such as <c>+1</c> and a region code such
/// as <c>US</c>.
/// </summary>
public static class PhoneCountryCodes
{
    /// <summary>
    /// Normalizes a calling code to its <c>+</c> form, such as <c>1</c> to <c>+1</c>.
    /// </summary>
    /// <param name="callingCode">The calling code, with or without the plus sign.</param>
    /// <returns>The calling code with a leading plus sign, or <see langword="null"/> when none is given.</returns>
    public static string NormalizeCallingCode(string callingCode)
    {
        if (string.IsNullOrWhiteSpace(callingCode))
        {
            return null;
        }

        var trimmed = callingCode.Trim();

        return trimmed.StartsWith('+') ? trimmed : "+" + trimmed;
    }

    /// <summary>
    /// Normalizes an ISO 3166-1 region code to upper case, such as <c>us</c> to <c>US</c>.
    /// </summary>
    /// <param name="regionCode">The region code.</param>
    /// <returns>The upper-case region code, or <see langword="null"/> when none is given.</returns>
    public static string NormalizeRegionCode(string regionCode)
        => string.IsNullOrWhiteSpace(regionCode) ? null : regionCode.Trim().ToUpperInvariant();
}
