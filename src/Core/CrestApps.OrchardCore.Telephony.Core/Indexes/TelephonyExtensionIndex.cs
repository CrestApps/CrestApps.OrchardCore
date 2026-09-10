using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Telephony.Core.Indexes;

/// <summary>
/// Indexes <see cref="Models.TelephonyExtension"/> documents for lookup by dialed number and by owning user.
/// The stable catalog identifier is provided by the <see cref="CatalogItemIndex"/> base as <c>ItemId</c>.
/// </summary>
public sealed class TelephonyExtensionIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the extension entry's display name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the normalized dialed extension number.
    /// </summary>
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the Orchard user the extension rings.
    /// </summary>
    public string UserId { get; set; }
}
