namespace CrestApps.OrchardCore.PhoneNumbers;

/// <summary>
/// Normalizes a phone number a person typed before it's kept on a record, so it matches the CRM, the do-not-call lists
/// and caller ID: the E.164 form when it parses, and the trimmed text as typed when it doesn't, so nothing a person
/// entered is lost.
/// </summary>
public static class PhoneNumberNormalization
{
    /// <summary>
    /// Returns <paramref name="rawNumber"/> in E.164 form when it parses, the trimmed text when it doesn't (or when no
    /// phone number service is available, such as with the Phone Numbers feature off), and <see langword="null"/> when
    /// it's empty.
    /// </summary>
    /// <param name="rawNumber">The number as typed.</param>
    /// <param name="phoneNumberService">The phone number service, or <see langword="null"/> when there's none.</param>
    /// <param name="regionCode">The ISO 3166-1 alpha-2 country a number without a country code is read in, such as the
    /// country of the record's address; or <see langword="null"/> to accept only numbers with a country code.</param>
    public static string ToE164OrTrimmed(string rawNumber, IPhoneNumberService phoneNumberService, string regionCode = null)
    {
        if (string.IsNullOrWhiteSpace(rawNumber))
        {
            return null;
        }

        var trimmed = rawNumber.Trim();

        if (PhoneNumber.IsE164(trimmed) || phoneNumberService is null)
        {
            return trimmed;
        }

        var region = string.IsNullOrWhiteSpace(regionCode) ? null : regionCode.Trim();

        return phoneNumberService.TryFormatToE164(trimmed, region, out var e164Number) && !string.IsNullOrEmpty(e164Number)
            ? e164Number
            : trimmed;
    }
}
