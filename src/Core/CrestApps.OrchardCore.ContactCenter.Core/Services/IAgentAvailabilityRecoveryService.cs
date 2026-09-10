namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Recovers agent capacity from orphaned or expired after-call work.
/// </summary>
public interface IAgentAvailabilityRecoveryService
{
    /// <summary>
    /// Completes orphaned or expired after-call work so agents do not remain unavailable indefinitely.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of agent wrap-up states recovered.</returns>
    Task<int> RecoverAsync(CancellationToken cancellationToken = default);
}
