using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Ranks eligible agents from the one handling the fewest active interactions to the most. Runs only when
/// the queue uses the <see cref="QueueRoutingStrategy.LeastBusy"/> strategy.
/// </summary>
/// <remarks>
/// The load comes from the availability snapshot the caller already read, so ranking N candidates costs no
/// round trips rather than N of them.
/// </remarks>
public sealed class LeastBusyRoutingStrategy : IActivityRoutingStrategy
{
    /// <inheritdoc/>
    public int Order => 100;

    /// <inheritdoc/>
    public ValueTask ApplyAsync(ActivityRoutingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Queue.RoutingStrategy != QueueRoutingStrategy.LeastBusy)
        {
            return ValueTask.CompletedTask;
        }

        var ranked = context.Candidates
            .Where(candidate => candidate.IsEligible)
            .Select(candidate => (Candidate: candidate, ActiveCount: candidate.Availability?.ActiveInteractionCount ?? 0))
            .OrderBy(load => load.ActiveCount)
            .ThenBy(load => load.Candidate.Agent.PresenceChangedUtc ?? DateTime.MaxValue)
            .ToArray();

        for (var index = 0; index < ranked.Length; index++)
        {
            var (candidate, activeCount) = ranked[index];
            candidate.Score += ranked.Length - index;
            candidate.AddReason($"Least-busy rank {index + 1} ({activeCount} active interactions).");
        }

        return ValueTask.CompletedTask;
    }
}
