using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Ranks eligible agents from the one handling the fewest active interactions to the most. Runs only when
/// the queue uses the <see cref="QueueRoutingStrategy.LeastBusy"/> strategy.
/// </summary>
public sealed class LeastBusyRoutingStrategy : IActivityRoutingStrategy
{
    private readonly IInteractionManager _interactionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeastBusyRoutingStrategy"/> class.
    /// </summary>
    /// <param name="interactionManager">The interaction manager used to count active agent interactions.</param>
    public LeastBusyRoutingStrategy(IInteractionManager interactionManager)
    {
        _interactionManager = interactionManager;
    }

    /// <inheritdoc/>
    public int Order => 100;

    /// <inheritdoc/>
    public async ValueTask ApplyAsync(ActivityRoutingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Queue.RoutingStrategy != QueueRoutingStrategy.LeastBusy)
        {
            return;
        }

        var eligibleCandidates = context.Candidates
            .Where(candidate => candidate.IsEligible)
            .ToArray();

        var loads = new List<(ActivityRoutingCandidate Candidate, int ActiveCount)>(eligibleCandidates.Length);

        foreach (var candidate in eligibleCandidates)
        {
            var activeCount = await _interactionManager.CountActiveByAgentAsync(candidate.Agent.ItemId, cancellationToken);
            loads.Add((candidate, activeCount));
        }

        var ranked = loads
            .OrderBy(load => load.ActiveCount)
            .ThenBy(load => load.Candidate.Agent.PresenceChangedUtc ?? DateTime.MaxValue)
            .ToArray();

        for (var index = 0; index < ranked.Length; index++)
        {
            var (candidate, activeCount) = ranked[index];
            candidate.Score += ranked.Length - index;
            candidate.AddReason($"Least-busy rank {index + 1} ({activeCount} active interactions).");
        }
    }
}
