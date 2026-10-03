namespace CrestApps.OrchardCore.Omnichannel.Core;

/// <summary>
/// The kinds of address an omnichannel address can be. The type decides how the value is stored and validated, and
/// which capabilities features can offer for it.
/// </summary>
public static class OmnichannelAddressTypes
{
    /// <summary>
    /// A phone number, stored in E.164 form. Calls and texts are its capabilities.
    /// </summary>
    public const string PhoneNumber = "PhoneNumber";

    /// <summary>
    /// An email address.
    /// </summary>
    public const string EmailAddress = "EmailAddress";

    /// <summary>
    /// Gets the address type of a record saved with a single channel before addresses had a type.
    /// </summary>
    /// <param name="channel">The record's channel.</param>
    /// <returns>The address type, or the channel itself for a channel another feature contributed.</returns>
    public static string FromChannel(string channel)
    {
        if (string.Equals(channel, OmnichannelConstants.Channels.Phone, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(channel, OmnichannelConstants.Channels.Sms, StringComparison.OrdinalIgnoreCase))
        {
            return PhoneNumber;
        }

        if (string.Equals(channel, OmnichannelConstants.Channels.Email, StringComparison.OrdinalIgnoreCase))
        {
            return EmailAddress;
        }

        return channel;
    }
}
