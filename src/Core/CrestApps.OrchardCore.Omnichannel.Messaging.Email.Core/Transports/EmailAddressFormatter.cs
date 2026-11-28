using MimeKit;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Transports;

/// <summary>
/// Writes an address with its display name the way a mail header expects it.
/// </summary>
public static class EmailAddressFormatter
{
    /// <summary>
    /// Formats an address with an optional display name, such as <c>"Contoso Support" &lt;support@contoso.com&gt;</c>.
    /// The name is quoted and encoded as the header rules require, so a name with commas or accents stays one name.
    /// </summary>
    /// <param name="address">The address.</param>
    /// <param name="name">The display name, if any.</param>
    /// <returns>The formatted address.</returns>
    public static string Format(string address, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return address;
        }

        return new MailboxAddress(name.Trim(), address).ToString(encode: true);
    }
}
