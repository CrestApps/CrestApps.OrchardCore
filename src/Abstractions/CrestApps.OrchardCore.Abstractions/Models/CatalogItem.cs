using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Models;

public class CatalogItem : Entity
{
    /// <summary>
    /// Gets or sets the unique identifier for the catalog item.
    /// </summary>
    public string ItemId { get; set; }
}
