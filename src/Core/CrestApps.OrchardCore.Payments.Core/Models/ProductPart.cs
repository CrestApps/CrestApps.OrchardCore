using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Payments.Core.Models;

public sealed class ProductPart : ContentPart
{
    /// <summary>
    /// The price of the item.
    /// </summary>
    public double Price { get; set; }
}
