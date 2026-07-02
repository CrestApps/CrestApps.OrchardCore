using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Scores eligible agents by how long they have been available. Runs only when the queue uses the
/// <see cref="QueueRoutingStrategy.LongestIdle"/> strategy.
/// </summary>
public sealed class LongestIdleRoutingStrategy : IActivityRoutingStrategy
{
    /// <inheritdoc/>
    public int Order => 100;

    /// <inheritdoc/>
    public ValueTask ApplyAsync(ActivityRoutingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Queue.RoutingStrategy != QueueRoutingStrategy.LongestIdle)
        {
            return ValueTask.CompletedTask;
        }

        var eligibleCandidates = context.Candidates
            .Where(candidate => candidate.IsEligible)
            .OrderBy(candidate => candidate.Agent.PresenceChangedUtc ?? DateTime.MaxValue)
            .ToArray();

        for (var index = 0; index < eligibleCandidates.Length; index++)
        {
            var candidate = eligibleCandidates[index];
            candidate.Score += eligibleCandidates.Length - index;
            candidate.AddReason($"Longest-idle rank {index + 1}.");
        }

        return ValueTask.CompletedTask;
    }
}
