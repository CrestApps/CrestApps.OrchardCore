using CrestApps.OrchardCore.Telephony.Sms.Models;

namespace CrestApps.OrchardCore.Telephony.Sms.Core.Services.Routers;

/// <summary>
/// Routes a new conversation to the target bound to its DID by an <c>SmsNumberRoute</c>: a single agent
/// (a personal inbox) or a queue (a department). This is the DID-is-the-routing-key step of the pipeline.
/// </summary>
public sealed class NumberRouteRouter : ISmsInboundRouter
{
    private readonly ISmsNumberRouteManager _numberRouteManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="NumberRouteRouter"/> class.
    /// </summary>
    /// <param name="numberRouteManager">The number-route manager used to resolve the route for the DID.</param>
    public NumberRouteRouter(ISmsNumberRouteManager numberRouteManager)
    {
        _numberRouteManager = numberRouteManager;
    }

    /// <inheritdoc/>
    public int Order => 300;

    /// <inheritdoc/>
    public async Task<bool> TryRouteAsync(SmsInboundRoutingContext context, CancellationToken cancellationToken = default)
    {
        if (!context.IsNewConversation)
        {
            return false;
        }

        var route = await _numberRouteManager.FindByDialedNumberAsync(context.Endpoint.Value, cancellationToken);

        if (route is null)
        {
            return false;
        }

        var conversation = context.Conversation;

        if (route.TargetType == SmsNumberRouteTargetType.Agent)
        {
            conversation.OwnerType = SmsConversationOwnerType.Personal;
            conversation.OwnerId = route.TargetId;
            conversation.AssignedAgentId = route.TargetId;
            conversation.AssignmentStatus = SmsConversationAssignmentStatus.Assigned;

            return true;
        }

        // Queue target: a "department". Phase 1 supports the shared-pool model (claim-to-own); Routed
        // reservation/assignment via the existing routing strategies lands in phase 2, so an unrecognized mode
        // falls back to the shared pool rather than dropping the message.
        conversation.OwnerType = SmsConversationOwnerType.Queue;
        conversation.OwnerId = route.TargetId;
        conversation.AssignedAgentId = null;
        conversation.AssignmentStatus = SmsConversationAssignmentStatus.Pooled;

        return true;
    }
}
