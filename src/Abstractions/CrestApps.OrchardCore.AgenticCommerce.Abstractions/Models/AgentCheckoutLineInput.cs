namespace CrestApps.OrchardCore.AgenticCommerce.Models;

/// <summary>
/// An item an agent puts in a checkout.
/// </summary>
public sealed class AgentCheckoutLineInput
{
    /// <summary>
    /// Gets or sets the item's id, as the store's product feed gives it.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets how many.
    /// </summary>
    public int Quantity { get; set; } = 1;
}
