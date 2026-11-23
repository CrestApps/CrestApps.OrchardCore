using CrestApps.OrchardCore.Emails;
using CrestApps.OrchardCore.PhoneNumbers;

namespace CrestApps.OrchardCore.Consent.Core;

/// <summary>
/// The form addresses are stored and compared in by the consent building block, so an opt-out recorded as
/// <c>Sam@Example.com </c> stops a send to <c>sam@example.com</c>.
/// </summary>
public static class ConsentAddresses
{
    /// <summary>
    /// Returns the address in its normalized form for the channel: an e-mail address trimmed and in lower case, a phone
    /// number in E.164 form when it parses (trimmed otherwise), any other address trimmed. Returns
    /// <see langword="null"/> when the address is empty.
    /// </summary>
    /// <param name="channel">The channel, one of <see cref="ConsentChannels"/>.</param>
    /// <param name="address">The address as it was given.</param>
    public static string Normalize(string channel, string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return null;
        }

        if (string.Equals(channel, ConsentChannels.Email, StringComparison.Ordinal))
        {
            return EmailAddresses.Normalize(address);
        }

        var trimmed = address.Trim();

        if (string.Equals(channel, ConsentChannels.Phone, StringComparison.Ordinal) ||
            string.Equals(channel, ConsentChannels.Sms, StringComparison.Ordinal))
        {
            return PhoneNumber.TryFromE164(trimmed, out var phoneNumber) && phoneNumber.HasValue
                ? phoneNumber.Value
                : trimmed;
        }

        return trimmed;
    }

    /// <summary>
    /// Returns the purpose trimmed, or <see langword="null"/> when it is empty (every purpose).
    /// </summary>
    /// <param name="purpose">The purpose as it was given.</param>
    public static string NormalizePurpose(string purpose)
        => string.IsNullOrWhiteSpace(purpose) ? null : purpose.Trim();
}
