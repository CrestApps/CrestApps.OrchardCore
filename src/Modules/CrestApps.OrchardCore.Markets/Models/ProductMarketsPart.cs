using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Markets.Models;

/// <summary>
/// The markets a product isn't sold in, kept on the product beside its product part.
/// </summary>
public sealed class ProductMarketsPart : ContentPart
{
    /// <summary>
    /// Gets or sets the ids of the markets the product isn't sold in.
    /// </summary>
    public List<string> NotSoldIn { get; set; } = [];
}
