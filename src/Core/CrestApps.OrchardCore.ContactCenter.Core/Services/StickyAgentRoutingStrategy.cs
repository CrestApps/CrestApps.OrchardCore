using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Boosts the eligible candidate who most recently owned the activity (the sticky agent) so that returning
/// work prefers the agent the customer already worked with. Runs only when the queue enables sticky routing.
/// </summary>
public sealed class StickyAgentRoutingStrategy : IActivityRoutingStrategy
{
    private const double _stickyAgentBoost = 1000d;

    /// <inheritdoc/>
    public int Order => 30;

    /// <inheritdoc/>
    public ValueTask ApplyAsync(ActivityRoutingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.Queue.PreferStickyAgent || string.IsNullOrEmpty(context.QueueItem.StickyAgentUserId))
        {
            return ValueTask.CompletedTask;
        }

        foreach (var candidate in context.Candidates)
        {
            if (!candidate.IsEligible)
            {
                continue;
            }

            if (string.Equals(candidate.Agent.UserId, context.QueueItem.StickyAgentUserId, StringComparison.Ordinal))
            {
                candidate.Score += _stickyAgentBoost;
                candidate.AddReason("Preferred sticky agent for this activity.");
            }
        }

        return ValueTask.CompletedTask;
    }
}
