namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// The numbers used when an agent has none of their own: the phone number calls are placed from and the number texts
/// are sent from. Each is an Omnichannel Address, chosen under Settings &gt; Contact Center.
/// </summary>
public sealed class ContactCenterDefaultAddressSettings
{
    /// <summary>
    /// Gets or sets the address of the default phone number.
    /// </summary>
    public string DefaultPhoneAddressId { get; set; }

    /// <summary>
    /// Gets or sets the address of the default SMS number.
    /// </summary>
    public string DefaultSmsAddressId { get; set; }
}
