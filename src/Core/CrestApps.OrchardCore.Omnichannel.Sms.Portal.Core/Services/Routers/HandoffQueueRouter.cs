using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services.Routing;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services.Routers;

/// <summary>
/// Places a thread an automated conversation escalated. The queue comes from the escalation request rather than
/// the endpoint, but the distribution decision is the endpoint's: on a routed queue the thread is push-assigned
/// to one agent exactly as a fresh inbound message would be, and anywhere else it is pooled. Escalations used to
/// pool unconditionally, so a routed department behaved like a shared one for the threads that most needed an
/// owner.
/// </summary>
public sealed class HandoffQueueRouter : ISmsInboundRouter
{
    private readonly ISmsRoutingStrategy _routingStrategy;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="HandoffQueueRouter"/> class.
    /// </summary>
    public HandoffQueueRouter(ISmsRoutingStrategy routingStrategy, IClock clock)
    {
        _routingStrategy = routingStrategy;
        _clock = clock;
    }

    /// <inheritdoc/>
    public int Order => 120;

    /// <inheritdoc/>
    public async Task<bool> TryRouteAsync(SmsRoutingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Trigger != SmsRoutingTrigger.Handoff || string.IsNullOrEmpty(context.TargetQueueId))
        {
            return false;
        }

        var conversation = context.Conversation;

        conversation.OwnerType = SmsConversationOwnerType.Queue;
        conversation.OwnerId = context.TargetQueueId;
        conversation.Status = SmsConversationStatus.Open;

        var agentId = IsRoutedQueueEndpoint(context)
            ? await _routingStrategy.SelectAgentAsync(context.TargetQueueId, cancellationToken: cancellationToken)
            : null;

        if (string.IsNullOrEmpty(agentId))
        {
            // A routed queue with nobody free still lands in the pool rather than refusing: the alternative is
            // leaving the customer in an automated conversation that has already given up on them.
            conversation.AssignedAgentId = null;
            conversation.AssignmentStatus = SmsConversationAssignmentStatus.Unassigned;
            conversation.AssignedUtc = null;
        }
        else
        {
            conversation.AssignedAgentId = agentId;
            conversation.AssignmentStatus = SmsConversationAssignmentStatus.Assigned;
            conversation.AssignedUtc = _clock.UtcNow;
        }

        return true;
    }

    private static bool IsRoutedQueueEndpoint(SmsRoutingContext context)
        => context.Endpoint is not null
            && context.Endpoint.TryGet<SmsEndpointRoutingSettings>(out var routing)
            && routing is not null
            && routing.TargetType == SmsNumberRouteTargetType.Queue
            && routing.DistributionMode == SmsNumberRouteDistributionMode.Routed;
}
