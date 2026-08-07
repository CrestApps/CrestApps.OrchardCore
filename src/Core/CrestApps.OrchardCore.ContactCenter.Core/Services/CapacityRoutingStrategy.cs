using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Rejects agents that are already handling their maximum number of concurrent interactions so that
/// routing never offers new work to an agent who is at capacity.
/// </summary>
public sealed class CapacityRoutingStrategy : IActivityRoutingStrategy
{
    private readonly IInteractionManager _interactionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="CapacityRoutingStrategy"/> class.
    /// </summary>
    /// <param name="interactionManager">The interaction manager used to count active agent interactions.</param>
    public CapacityRoutingStrategy(IInteractionManager interactionManager)
    {
        _interactionManager = interactionManager;
    }

    /// <inheritdoc/>
    public int Order => 20;

    /// <inheritdoc/>
    public async ValueTask ApplyAsync(ActivityRoutingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var candidate in context.Candidates)
        {
            if (!candidate.IsEligible)
            {
                continue;
            }

            var capacity = candidate.Agent.MaxConcurrentInteractions > 0
                ? candidate.Agent.MaxConcurrentInteractions
                : 1;

            var activeCount = await _interactionManager.CountActiveByAgentAsync(candidate.Agent.ItemId, cancellationToken);

            if (activeCount >= capacity)
            {
                candidate.IsEligible = false;
                candidate.AddReason($"At capacity ({activeCount}/{capacity} active interactions).");
            }
            else
            {
                candidate.AddReason($"Has spare capacity ({activeCount}/{capacity} active interactions).");
            }
        }
    }
}
