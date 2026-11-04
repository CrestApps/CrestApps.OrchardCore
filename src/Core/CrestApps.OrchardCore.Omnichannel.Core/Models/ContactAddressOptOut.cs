using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// A request not to be reached on a channel, recorded against the address (for example a phone number) rather than a
/// contact.
/// </summary>
/// <remarks>
/// A person who texts STOP from a number that no contact record holds is still owed silence on that number. The
/// opt-out is kept here so the send paths can honor it, through <see cref="Services.IContactOptOutResolver"/>, until
/// the person opts back in.
/// </remarks>
public sealed class ContactAddressOptOut : CatalogItem
{
    /// <summary>
    /// Gets or sets the channel the person asked not to be reached on, one of <see cref="OmnichannelConstants.Channels"/>.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets the address, in E.164 form for a phone number.
    /// </summary>
    public string Address { get; set; }

    /// <summary>
    /// Gets or sets what recorded the opt-out (for example <c>SmsKeyword</c> or <c>ProviderRefusal</c>).
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets when the opt-out was recorded.
    /// </summary>
    public DateTime OptedOutUtc { get; set; }
}
