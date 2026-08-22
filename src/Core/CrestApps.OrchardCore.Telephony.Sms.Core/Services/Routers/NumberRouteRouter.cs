using CrestApps.Core;
using CrestApps.OrchardCore.Telephony.Sms.Core.Models;
using CrestApps.OrchardCore.Telephony.Sms.Models;

namespace CrestApps.OrchardCore.Telephony.Sms.Core.Services.Routers;

/// <summary>
/// Routes a new conversation to the target configured on its DID's channel endpoint (Agent or Queue). This is
/// the DID-is-the-routing-key step of the pipeline; the routing lives on the <c>OmnichannelChannelEndpoint</c>
/// itself rather than in a separate catalog.
/// </summary>
public sealed class NumberRouteRouter : ISmsInboundRouter
{
    /// <inheritdoc/>
    public int Order => 300;

    /// <inheritdoc/>
    public Task<bool> TryRouteAsync(SmsInboundRoutingContext context, CancellationToken cancellationToken = default)
    {
        if (!context.IsNewConversation)
        {
            return Task.FromResult(false);
        }

        if (!context.Endpoint.TryGet<SmsEndpointRoutingSettings>(out var routing) ||
            routing is null ||
            string.IsNullOrEmpty(routing.TargetId))
        {
            return Task.FromResult(false);
        }

        var conversation = context.Conversation;

        if (routing.TargetType == SmsNumberRouteTargetType.Agent)
        {
            conversation.OwnerType = SmsConversationOwnerType.Personal;
            conversation.OwnerId = routing.TargetId;
            conversation.AssignedAgentId = routing.TargetId;
            conversation.AssignmentStatus = SmsConversationAssignmentStatus.Assigned;

            return Task.FromResult(true);
        }

        // Queue target ("department"). Phase 1 supports the shared-pool model (claim-to-own); Routed
        // reservation/assignment lands in a later phase, so an unrecognized mode falls back to the shared pool
        // rather than dropping the message.
        conversation.OwnerType = SmsConversationOwnerType.Queue;
        conversation.OwnerId = routing.TargetId;
        conversation.AssignedAgentId = null;
        conversation.AssignmentStatus = SmsConversationAssignmentStatus.Pooled;

        return Task.FromResult(true);
    }
}
