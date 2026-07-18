namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// Represents a pending registration for a single agent channel's readiness. Disposing the registration
/// releases the underlying waiter so it can never leak across connect attempts.
/// </summary>
internal interface IAsteriskAgentChannelReadyRegistration : IDisposable
{
    /// <summary>
    /// Waits until the registered agent channel becomes ready, the timeout elapses, or the operation is cancelled.
    /// </summary>
    /// <param name="timeout">The maximum time to wait for the channel to become ready.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the channel became ready in time; otherwise, <see langword="false"/>.</returns>
    Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken);
}
