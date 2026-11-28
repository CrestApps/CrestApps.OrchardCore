using CrestApps.OrchardCore.Orders.Core.Models;

namespace CrestApps.OrchardCore.Orders.Services;

/// <summary>
/// Plans where a refund of an order would go, without refunding anything: the same payments, in the same order, that
/// <see cref="OrderOperationsService.RefundAsync(Order, decimal?, string, bool, string, bool, CancellationToken)"/> refunds.
/// <see cref="OrderOperationsService"/> implements it next to the refund, so the two stay in step.
/// </summary>
internal interface IOrderRefundPlanner
{
    /// <summary>
    /// Plans a refund of <paramref name="amount"/> for the order: as store credit, or newest online payment first, then
    /// Pay Later and offline payments, then what a gift card or store credit paid. Nothing is refunded or saved.
    /// </summary>
    /// <param name="order">The order.</param>
    /// <param name="amount">The amount to refund, in the order's currency.</param>
    /// <param name="asStoreCredit">Whether the refund would be given as store credit.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>Where the amount would go, what would be skipped, and what no payment would cover.</returns>
    Task<OrderRefundPlan> PlanRefundAsync(Order order, decimal amount, bool asStoreCredit, CancellationToken cancellationToken = default);
}

/// <summary>
/// Where a refund of an order would go.
/// </summary>
internal sealed class OrderRefundPlan
{
    /// <summary>
    /// Gets the parts of the amount, in the order they would be refunded.
    /// </summary>
    public List<OrderRefundPortion> Portions { get; } = [];

    /// <summary>
    /// Gets the online payments the refund would skip, because a refund of them waits to be settled by hand, with what
    /// each could still give back.
    /// </summary>
    public List<OrderRefundPortion> Skipped { get; } = [];

    /// <summary>
    /// Gets or sets the part of the amount no payment would cover, such as when a payment is skipped.
    /// </summary>
    public decimal NotCovered { get; set; }
}

/// <summary>
/// A part of a refund and where it would go.
/// </summary>
/// <param name="Kind">What the part goes back to.</param>
/// <param name="Description">The payment provider, the transaction, or the balances it goes back to.</param>
/// <param name="Amount">How much of the refund goes there.</param>
/// <param name="PaidUtc">When that payment was made, when known.</param>
/// <param name="ProviderSupportsRefunds">For an online payment, whether its provider says it can refund; otherwise <see langword="null"/>.</param>
internal sealed record OrderRefundPortion(
    OrderRefundPortionKind Kind,
    string Description,
    decimal Amount,
    DateTime? PaidUtc,
    bool? ProviderSupportsRefunds);

/// <summary>
/// What a part of a refund goes back to.
/// </summary>
internal enum OrderRefundPortionKind
{
    /// <summary>
    /// A payment made online at checkout, refunded through its payment provider.
    /// </summary>
    Payment,

    /// <summary>
    /// A payment recorded on the order's transaction, such as Pay Later or a bank transfer.
    /// </summary>
    Transaction,

    /// <summary>
    /// The gift cards or store credit the checkout used.
    /// </summary>
    Balance,

    /// <summary>
    /// Store credit for the buyer, instead of the payments.
    /// </summary>
    StoreCredit,
}
