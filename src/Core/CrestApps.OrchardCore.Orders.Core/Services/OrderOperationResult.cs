namespace CrestApps.OrchardCore.Orders.Services;

/// <summary>
/// The outcome of an operation on an order.
/// </summary>
public sealed class OrderOperationResult
{
    /// <summary>
    /// Gets whether the operation succeeded: no errors were reported.
    /// </summary>
    public bool Succeeded
        => Errors.Count == 0;

    /// <summary>
    /// Gets or sets what was refunded.
    /// </summary>
    public decimal Refunded { get; set; }

    /// <summary>
    /// Gets what the operation put back on balances, such as a gift card, store credit or the cash a till sale was paid
    /// with, by the key of the balance's tender provider.
    /// </summary>
    public Dictionary<string, decimal> ReturnedToBalances { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets why the operation, or part of it, failed.
    /// </summary>
    public List<string> Errors { get; } = [];

    /// <summary>
    /// Gets what the store should know about the operation, such as a refund still being processed.
    /// </summary>
    public List<string> Messages { get; } = [];
}
