using CrestApps.OrchardCore.Omnichannel.Core;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Inbound;

/// <summary>
/// An address on a received email, with the display name it was written under.
/// </summary>
public sealed class InboundEmailAddress
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InboundEmailAddress"/> class.
    /// </summary>
    public InboundEmailAddress()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InboundEmailAddress"/> class.
    /// </summary>
    /// <param name="address">The address, in any form <see cref="OmnichannelEmailAddress.Normalize"/> accepts.</param>
    /// <param name="name">The display name, if any.</param>
    public InboundEmailAddress(string address, string name = null)
    {
        Address = OmnichannelEmailAddress.Normalize(address);
        Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim().Trim('"').Trim();
    }

    /// <summary>
    /// Gets or sets the address, in its canonical form.
    /// </summary>
    public string Address { get; set; }

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string Name { get; set; }
}
