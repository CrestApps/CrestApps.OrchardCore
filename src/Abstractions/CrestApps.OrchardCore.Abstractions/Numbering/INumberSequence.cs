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

    /// <summary>
    /// Returns the last number a series issued, without issuing one.
    /// </summary>
    /// <param name="series">The name of the series.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The last number, or <see langword="null"/> when the series has issued none.</returns>
    Task<long?> GetLastAsync(string series, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the number a series issues next, for a series that follows numbers printed elsewhere, such as a new box of
    /// check stock. The change is saved in the caller's unit of work, under the same lock as
    /// <see cref="NextAsync(string, NumberSequenceOptions, CancellationToken)"/>. The series does not know which numbers
    /// are in use: a caller that sets a lower number than the last one must make sure its own records do not repeat.
    /// </summary>
    /// <param name="series">The name of the series.</param>
    /// <param name="next">The number to issue next; 1 or more.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task SetNextAsync(string series, long next, CancellationToken cancellationToken = default);
}
