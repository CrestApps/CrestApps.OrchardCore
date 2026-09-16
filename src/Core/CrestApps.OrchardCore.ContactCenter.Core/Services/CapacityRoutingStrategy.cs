using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Rejects agents that are already handling their maximum number of concurrent interactions so that
/// routing never offers new work to an agent who is at capacity.
/// </summary>
/// <remarks>
/// The active count comes from the availability snapshot the caller already read, not from a query per
/// candidate. Reading it per candidate made one assignment cost a round trip for every signed-in agent, which
/// grows with the size of the queue rather than with the work being assigned.
/// </remarks>
public sealed class CapacityRoutingStrategy : IActivityRoutingStrategy
{
    /// <inheritdoc/>
    public int Order => 20;

    /// <inheritdoc/>
    public ValueTask ApplyAsync(ActivityRoutingContext context, CancellationToken cancellationToken = default)
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

            var activeCount = candidate.Availability?.ActiveInteractionCount ?? 0;

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

        return ValueTask.CompletedTask;
    }
}
