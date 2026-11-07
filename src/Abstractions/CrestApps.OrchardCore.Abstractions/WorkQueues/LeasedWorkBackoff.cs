namespace CrestApps.OrchardCore.WorkQueues;

/// <summary>
/// Computes the wait before the next attempt of a failed work item, so queues stop hand-rolling back-off formulas.
/// </summary>
public static class LeasedWorkBackoff
{
    /// <summary>
    /// Returns an exponential back-off policy for <see cref="ILeasedWorkQueue{T}.RetryAsync"/>.
    /// </summary>
    /// <param name="baseDelay">The wait after the first failure.</param>
    /// <param name="maxDelay">The longest wait.</param>
    /// <returns>The policy, which maps the attempt count after a failure to the wait before the next attempt.</returns>
    public static Func<int, TimeSpan> Exponential(TimeSpan baseDelay, TimeSpan maxDelay)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(baseDelay, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxDelay, baseDelay);

        return attempt => Exponential(attempt, baseDelay, maxDelay);
    }

    /// <summary>
    /// Returns an exponential back-off: <paramref name="baseDelay"/> for the first attempt, doubled for each later one,
    /// and never more than <paramref name="maxDelay"/>.
    /// </summary>
    /// <param name="attempt">The number of the attempt that failed, starting at 1. Values below 1 count as 1.</param>
    /// <param name="baseDelay">The wait after the first failure.</param>
    /// <param name="maxDelay">The longest wait.</param>
    /// <returns>The wait before the next attempt.</returns>
    public static TimeSpan Exponential(int attempt, TimeSpan baseDelay, TimeSpan maxDelay)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(baseDelay, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxDelay, baseDelay);

        var exponent = Math.Clamp(attempt - 1, 0, 30);
        var seconds = Math.Min(baseDelay.TotalSeconds * Math.Pow(2, exponent), maxDelay.TotalSeconds);

        return TimeSpan.FromSeconds(seconds);
    }
}
