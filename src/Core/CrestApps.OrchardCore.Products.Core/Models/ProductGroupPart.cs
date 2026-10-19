using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Products.Core.Models;

/// <summary>
/// The products a grouped product shows together, such as the pieces of a set, which a shopper buys in the
/// quantities they choose from the one page. The grouped product itself isn't bought.
/// </summary>
public sealed class ProductGroupPart : ContentPart
{
    /// <summary>
    /// The most products a group can show.
    /// </summary>
    public const int MaxProducts = 20;

    public List<string> ProductIds { get; set; } = [];
}
