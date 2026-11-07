namespace CrestApps.OrchardCore.Orders.Services;

/// <summary>
/// What can still be refunded for an order, and what is still owed for it.
/// </summary>
public sealed class OrderMoney
{
    /// <summary>
    /// Gets or sets whether the refund can be given to the buyer as store credit.
    /// </summary>
    public bool CanRefundAsStoreCredit { get; set; }

    /// <summary>
    /// Gets or sets the order's currency.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets what can still be refunded.
    /// </summary>
    public decimal Refundable { get; set; }

    /// <summary>
    /// Gets or sets what is still owed, such as through Pay Later or a bank transfer.
    /// </summary>
    public decimal Outstanding { get; set; }
}
