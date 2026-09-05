namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// The default <see cref="IAgentWorkStateHealingService"/> for a tenant without the Queues feature. There are no
/// queues for work to be stranded in, so there is nothing to heal.
/// </summary>
public sealed class NoAgentWorkStateHealingService : IAgentWorkStateHealingService
{
    /// <inheritdoc/>
    public Task<int> HealForResetAsync(string agentId, CancellationToken cancellationToken = default)
        => Task.FromResult(0);

    /// <inheritdoc/>
    public Task<int> HealForAvailabilityAsync(string agentId, CancellationToken cancellationToken = default)
        => Task.FromResult(0);
}
