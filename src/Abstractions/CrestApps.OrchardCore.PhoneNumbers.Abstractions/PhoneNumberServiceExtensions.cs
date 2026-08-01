namespace CrestApps.OrchardCore.PhoneNumbers;

/// <summary>
/// The canonical entry point for turning a raw phone number into a <see cref="PhoneNumber"/>.
/// </summary>
/// <remarks>
/// Every call site used to repair a number that failed to parse in its own way — one stripped everything but
/// the digits, one kept the raw text, one dropped the number entirely — so the same number reached storage in
/// one form and a lookup in another and the two never matched. The failure was worst where it mattered most:
/// a destination that could not be canonicalized was checked against the do-not-call registries as a
/// digits-only string, the registry could not canonicalize it either and dropped it, and the call was placed
/// because nothing was found. There is therefore exactly one way to obtain a number, and it either produces a
/// canonical one or produces nothing; a caller that gets nothing has to decide what to do about it in the
/// open rather than substitute a value that only looks close enough.
/// </remarks>
public static class PhoneNumberServiceExtensions
{
    /// <summary>
    /// Attempts to parse a raw phone number into its canonical E.164 form.
    /// </summary>
    /// <param name="service">The phone number service.</param>
    /// <param name="rawNumber">The raw number as it was entered, imported, or reported by a provider.</param>
    /// <param name="regionCode">
    /// The ISO 3166-1 alpha-2 region the number should be read in when it carries no country calling code.
    /// It may be <see langword="null"/> only when the number is already international.
    /// </param>
    /// <param name="phoneNumber">The canonical number when parsing succeeds; otherwise the default value.</param>
    /// <returns><see langword="true"/> when the raw number is a valid number; otherwise <see langword="false"/>.</returns>
    public static bool TryParse(this IPhoneNumberService service, string rawNumber, string regionCode, out PhoneNumber phoneNumber)
    {
        ArgumentNullException.ThrowIfNull(service);

        phoneNumber = default;

        if (string.IsNullOrWhiteSpace(rawNumber))
        {
            return false;
        }

        if (!service.TryFormatToE164(rawNumber, regionCode, out var e164Number))
        {
            return false;
        }

        return PhoneNumber.TryFromE164(e164Number, out phoneNumber);
    }

    /// <summary>
    /// Attempts to parse a raw phone number that is expected to already carry a country calling code.
    /// </summary>
    /// <param name="service">The phone number service.</param>
    /// <param name="rawNumber">The raw number as it was entered, imported, or reported by a provider.</param>
    /// <param name="phoneNumber">The canonical number when parsing succeeds; otherwise the default value.</param>
    /// <returns><see langword="true"/> when the raw number is a valid number; otherwise <see langword="false"/>.</returns>
    public static bool TryParse(this IPhoneNumberService service, string rawNumber, out PhoneNumber phoneNumber)
        => service.TryParse(rawNumber, regionCode: null, out phoneNumber);

    /// <summary>
    /// Gets the ISO 3166-1 alpha-2 region code of a canonical number.
    /// </summary>
    /// <param name="service">The phone number service.</param>
    /// <param name="phoneNumber">The canonical number.</param>
    /// <returns>The region code, or <see langword="null"/> when it cannot be determined.</returns>
    public static string GetRegionCode(this IPhoneNumberService service, PhoneNumber phoneNumber)
    {
        ArgumentNullException.ThrowIfNull(service);

        if (!phoneNumber.HasValue)
        {
            return null;
        }

        return service.GetRegionCode(phoneNumber.Value);
    }
}
