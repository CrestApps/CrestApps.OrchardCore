using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services.Routing;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services.Routers;

/// <summary>
/// Places a routed thread again after the agent it was pushed to did not pick it up. It prefers another eligible
/// agent, excluding the one who let it sit, and falls back to the queue's shared pool once the attempt budget is
/// spent, so a thread successive agents ignore cannot bounce between them forever.
/// </summary>
public sealed class ReassignmentRouter : IMessagingInboundRouter
{
    private readonly IMessagingRoutingStrategy _routingStrategy;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReassignmentRouter"/> class.
    /// </summary>
    public ReassignmentRouter(IMessagingRoutingStrategy routingStrategy, IClock clock)
    {
        _routingStrategy = routingStrategy;
        _clock = clock;
    }

    /// <inheritdoc/>
    public int Order => 110;

    /// <inheritdoc/>
    public async Task<bool> TryRouteAsync(MessagingRoutingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Trigger != MessagingRoutingTrigger.Reassignment)
        {
            return false;
        }

        var conversation = context.Conversation;
        var now = _clock.UtcNow;

        var nextAgentId = conversation.ReassignmentAttempts < context.MaxReassignmentAttempts
            ? await _routingStrategy.SelectAgentAsync(conversation.OwnerId, excludeAgentId: context.ExcludeAgentId, cancellationToken)
            : null;

        if (string.IsNullOrEmpty(nextAgentId))
        {
            // Pooled, not Unassigned: the thread was placed and came back, which is a different state from one
            // that was never placed, and the inbox filters distinguish them.
            conversation.AssignmentStatus = ConversationAssignmentStatus.Pooled;
            conversation.AssignedAgentId = null;
            conversation.AssignedUtc = null;
            conversation.ReassignmentAttempts = 0;
        }
        else
        {
            conversation.AssignedAgentId = nextAgentId;
            conversation.AssignmentStatus = ConversationAssignmentStatus.Assigned;
            conversation.AssignedUtc = now;
            conversation.ReassignmentAttempts++;
        }

        conversation.ModifiedUtc = now;

        return true;
    }
}
