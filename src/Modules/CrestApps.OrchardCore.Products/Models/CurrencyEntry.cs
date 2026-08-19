using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;

namespace CrestApps.OrchardCore.Products.Models;

/// <summary>
/// Represents a managed currency that editors can reuse across products and subscriptions.
/// </summary>
public sealed class CurrencyEntry : CatalogItem, INameAwareModel, IModifiedUtcAwareModel, ICloneable<CurrencyEntry>
{
    /// <summary>
    /// Gets or sets the ISO-4217 currency code.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the friendly display name shown to editors.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the created UTC timestamp.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the modified UTC timestamp.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets the author user name.
    /// </summary>
    public string Author { get; set; }

    /// <summary>
    /// Gets or sets the owner identifier.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Creates a copy of the current currency entry.
    /// </summary>
    public CurrencyEntry Clone()
    {
        return new CurrencyEntry
        {
            ItemId = ItemId,
            Name = Name,
            DisplayName = DisplayName,
            CreatedUtc = CreatedUtc,
            ModifiedUtc = ModifiedUtc,
            Author = Author,
            OwnerId = OwnerId,
        };
    }
}
