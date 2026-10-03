using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Something the business can do with an address, contributed by the feature that does it: calls and texts on a phone
/// number, for example. The address editor lists the registered capabilities of the address's type as checkboxes, and
/// the settings a feature keeps on an address are shown only while its capability is ticked.
/// </summary>
public sealed class OmnichannelAddressCapability
{
    /// <summary>
    /// Gets or sets the capability's name, which is the channel it serves (for example <c>Phone</c> or <c>SMS</c>).
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the address type the capability applies to (see <see cref="OmnichannelAddressTypes"/>).
    /// </summary>
    public string AddressType { get; set; }

    /// <summary>
    /// Gets or sets the localized name shown next to the capability's checkbox.
    /// </summary>
    public LocalizedString DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the localized hint shown under the capability's checkbox.
    /// </summary>
    public LocalizedString Description { get; set; }
}
