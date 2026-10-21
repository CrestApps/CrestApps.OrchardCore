namespace CrestApps.OrchardCore.Orders.Core.Models;

/// <summary>
/// The order bump a buyer added on the payment step: the product and the price it was offered at. Completing the
/// checkout adds it to the order as a line.
/// </summary>
public sealed class CheckoutOrderBump
{
    /// <summary>
    /// The id of the billing line the bump adds to the order's charges.
    /// </summary>
    public const string BillingItemId = "order-bump";

    public string ContentItemId { get; set; }

    public string ContentItemVersionId { get; set; }

    public string ContentType { get; set; }

    public string Title { get; set; }

    public string Sku { get; set; }

    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Gets or sets the product's own price, which the offer may take a discount off.
    /// </summary>
    public decimal ListPrice { get; set; }

    public Products.Core.Models.ProductType ProductType { get; set; }

    public string TaxCategoryCode { get; set; }

    public string TaxClassificationCode { get; set; }

    public string[] CategoryIds { get; set; } = [];
}
