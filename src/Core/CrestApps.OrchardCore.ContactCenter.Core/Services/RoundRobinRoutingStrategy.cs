using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Distributes work fairly by ranking eligible agents from the one who least recently received an
/// assignment to the most recent. Runs only when the queue uses the <see cref="QueueRoutingStrategy.RoundRobin"/>
/// strategy.
/// </summary>
public sealed class RoundRobinRoutingStrategy : IActivityRoutingStrategy
{
    /// <inheritdoc/>
    public int Order => 100;

    /// <inheritdoc/>
    public ValueTask ApplyAsync(ActivityRoutingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Queue.RoutingStrategy != QueueRoutingStrategy.RoundRobin)
        {
            return ValueTask.CompletedTask;
        }

        var eligibleCandidates = context.Candidates
            .Where(candidate => candidate.IsEligible)
            .OrderBy(candidate => candidate.Agent.LastAssignedUtc ?? DateTime.MinValue)
            .ThenBy(candidate => candidate.Agent.PresenceChangedUtc ?? DateTime.MaxValue)
            .ToArray();

        for (var index = 0; index < eligibleCandidates.Length; index++)
        {
            var candidate = eligibleCandidates[index];
            candidate.Score += eligibleCandidates.Length - index;
            candidate.AddReason($"Round-robin rank {index + 1}.");
        }

        return ValueTask.CompletedTask;
    }
}
