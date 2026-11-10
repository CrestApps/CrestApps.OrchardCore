namespace CrestApps.OrchardCore.AgenticCommerce.Models;

/// <summary>
/// An item of an agent checkout, priced.
/// </summary>
public sealed class AgentCheckoutLineView
{
    /// <summary>
    /// Gets or sets the line's id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the item's id, as the store's product feed gives it.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the item's title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the address of the item's image.
    /// </summary>
    public string ImageUrl { get; set; }

    /// <summary>
    /// Gets or sets the address of the item's page.
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// Gets or sets how many.
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Gets or sets the price of one, before discounts.
    /// </summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Gets or sets the price of all of them, before discounts.
    /// </summary>
    public decimal Subtotal { get; set; }

    /// <summary>
    /// Gets or sets the discount on the line.
    /// </summary>
    public decimal Discount { get; set; }

    /// <summary>
    /// Gets or sets the line's total, after discounts.
    /// </summary>
    public decimal Total { get; set; }
}
