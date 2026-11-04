using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Inventory.Core.Models;

/// <summary>
/// A place stock is kept: a warehouse or shop, or one owned by another record, such as a van. Its item id is the
/// location id stock levels and movements refer to.
/// </summary>
public sealed class StockLocation : CatalogItem
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the address, as one line.
    /// </summary>
    public string Address { get; set; }

    /// <summary>
    /// Gets or sets its place in the order orders take units from the site's locations, from 1.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Gets or sets the kind of record that owns it, such as <c>Asset</c>, or <see langword="null"/> for a site's own
    /// location.
    /// </summary>
    public string OwnerKind { get; set; }

    /// <summary>
    /// Gets or sets the record that owns it.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets when it was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}
