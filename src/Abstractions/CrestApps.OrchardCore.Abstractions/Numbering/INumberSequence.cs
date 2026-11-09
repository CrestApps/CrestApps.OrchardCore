namespace CrestApps.OrchardCore.Numbering;

/// <summary>
/// Issues the numbers of a named series (orders, returns, purchase orders, visits, work orders) one after another,
/// under a lock, so two requests or two nodes never get the same number. A number is saved in the caller's unit of work:
/// when that work fails, the number is not used and the next issuer gets it, so the series has no gaps.
/// </summary>
public interface INumberSequence
{
    /// <summary>
    /// Returns the next number of a series.
    /// </summary>
    /// <param name="series">The name of the series, such as <c>Orders.Order</c>. Each name has its own numbers.</param>
    /// <param name="options">Where a new series starts; <see langword="null"/> starts it at 1.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number.</returns>
    Task<long> NextAsync(string series, NumberSequenceOptions options = null, CancellationToken cancellationToken = default);
}
