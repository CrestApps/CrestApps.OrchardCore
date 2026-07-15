using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Recovers agent capacity when after-call work is orphaned or exceeds the configured deadline.
/// </summary>
public sealed class AgentAvailabilityRecoveryService : IAgentAvailabilityRecoveryService
{
    private readonly IAgentProfileManager _agentManager;
    private readonly IInteractionManager _interactionManager;
    private readonly IAgentPresenceManager _presenceManager;
    private readonly AgentAvailabilityOptions _options;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentAvailabilityRecoveryService"/> class.
    /// </summary>
    /// <param name="agentManager">The agent profile manager.</param>
    /// <param name="interactionManager">The interaction manager.</param>
    /// <param name="presenceManager">The agent presence manager.</param>
    /// <param name="options">The availability policy.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public AgentAvailabilityRecoveryService(
        IAgentProfileManager agentManager,
        IInteractionManager interactionManager,
        IAgentPresenceManager presenceManager,
        IOptions<AgentAvailabilityOptions> options,
        IClock clock,
        ILogger<AgentAvailabilityRecoveryService> logger)
    {
        _agentManager = agentManager;
        _interactionManager = interactionManager;
        _presenceManager = presenceManager;
        _options = options.Value;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<int> RecoverAsync(CancellationToken cancellationToken = default)
    {
        var agents = await _agentManager.ListByPresenceAsync(AgentPresenceStatus.WrapUp, cancellationToken);
        var recovered = 0;

        foreach (var agent in agents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var interactions = await _interactionManager.ListPendingWrapUpsByAgentAsync(agent.ItemId, cancellationToken);

            if (interactions.Any(interaction =>
                interaction.WrapUpStartedUtc.HasValue &&
                interaction.WrapUpStartedUtc.Value + _options.MaximumWrapUpDuration > _clock.UtcNow))
            {
                continue;
            }

            foreach (var interaction in interactions)
            {
                interaction.WrapUpCompletedUtc = _clock.UtcNow;
                await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);
            }

            AgentProfile updated;

            try
            {
                updated = await _presenceManager.CompleteWorkAsync(agent.ItemId, cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(
                    OperationalLogRedactor.RedactException(ex),
                    "Skipped availability recovery for contended Contact Center agent '{AgentId}'.",
                    OperationalLogRedactor.Pseudonymize(agent.ItemId, OperationalLogIdentifierCategory.Agent));

                continue;
            }

            if (updated is null)
            {
                continue;
            }

            recovered++;
            _logger.LogWarning(
                "Recovered expired or orphaned after-call work for Contact Center agent '{AgentId}'.",
                OperationalLogRedactor.Pseudonymize(agent.ItemId, OperationalLogIdentifierCategory.Agent));
        }

        return recovered;
    }
}
