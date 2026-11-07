using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Core.Indexes;

/// <summary>
/// The index of <see cref="Models.ContactAddressOptOut"/> records, looked up by channel and address before a send.
/// </summary>
public sealed class ContactAddressOptOutIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the channel the opt-out applies to.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets the address, in E.164 form for a phone number.
    /// </summary>
    public string Address { get; set; }
}
