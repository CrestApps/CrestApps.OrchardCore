namespace CrestApps.OrchardCore.PhoneNumbers;

/// <summary>
/// Normalizes a phone number a person typed before it's kept on a record, so it matches the CRM, the do-not-call lists
/// and caller ID: the E.164 form when it parses, and the trimmed text as typed when it doesn't, so nothing a person
/// entered is lost.
/// </summary>
/// <remarks>
/// A number is only normalized when its E.164 form keeps everything that was typed: text with an extension (such as
/// <c>x12</c>, <c>ext. 12</c> or <c>#12</c>), a second number or a note is kept as typed, and so is a number whose E.164
/// form would drop digits.
/// </remarks>
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

        // Text other than digits and the usual separators (an extension such as "x12", "ext. 12" or "#12", a second
        // number, a note) would be dropped by the E.164 form, so the number is kept as typed.
        if (!HasOnlyNumberCharacters(trimmed))
        {
            return trimmed;
        }

        var region = string.IsNullOrWhiteSpace(regionCode) ? null : regionCode.Trim();

        return phoneNumberService.TryFormatToE164(trimmed, region, out var e164Number) &&
            !string.IsNullOrEmpty(e164Number) &&
            KeepsEveryDigit(trimmed, e164Number)
                ? e164Number
                : trimmed;
    }

    private static bool HasOnlyNumberCharacters(string text)
    {
        foreach (var character in text)
        {
            if (!char.IsAsciiDigit(character) && character is not (' ' or '+' or '-' or '(' or ')' or '.'))
            {
                return false;
            }
        }

        return true;
    }

    // The E.164 form must end with every digit typed, once the leading zeros of a trunk prefix (0) or an international
    // prefix (00) are left out; otherwise digits were dropped, and the number is kept as typed.
    private static bool KeepsEveryDigit(string typed, string e164Number)
    {
        var typedDigits = new string([.. typed.Where(char.IsAsciiDigit)]).TrimStart('0');
        var e164Digits = new string([.. e164Number.Where(char.IsAsciiDigit)]);

        return typedDigits.Length > 0 && e164Digits.EndsWith(typedDigits, StringComparison.Ordinal);
    }
}
