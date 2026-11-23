using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Consent.Core.Indexes;

/// <summary>
/// The index of <see cref="Models.ConsentAddressOptOut"/> records, looked up by channel, address and purpose before a
/// send.
/// </summary>
public sealed class ConsentAddressOptOutIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the document's id, which an export pages through the records in the order of.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the channel the opt-out applies to.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets the normalized address.
    /// </summary>
    public string Address { get; set; }

    /// <summary>
    /// Gets or sets the purpose the person stopped.
    /// </summary>
    public string Purpose { get; set; }
}
