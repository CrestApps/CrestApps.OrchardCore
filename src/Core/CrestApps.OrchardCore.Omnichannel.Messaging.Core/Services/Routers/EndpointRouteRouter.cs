using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services.Routers;

/// <summary>
/// Routes a new conversation to the target of the inbound entry point that answers its number (Agent or Queue). This
/// is the number-is-the-routing-key step of the pipeline.
/// </summary>
public sealed class EndpointRouteRouter : IMessagingInboundRouter
{
    /// <inheritdoc/>
    public int Order => 300;

    /// <inheritdoc/>
    public Task<bool> TryRouteAsync(MessagingRoutingContext context, CancellationToken cancellationToken = default)
    {
        if (!context.IsNewConversation)
        {
            return Task.FromResult(false);
        }

        var routing = context.Routing;

        if (routing is null || string.IsNullOrEmpty(routing.TargetId))
        {
            return Task.FromResult(false);
        }

        var conversation = context.Conversation;

        if (routing.TargetType == ConversationRouteTargetType.Agent)
        {
            conversation.OwnerType = ConversationOwnerType.Personal;
            conversation.OwnerId = routing.TargetId;
            conversation.AssignedAgentId = routing.TargetId;
            conversation.AssignmentStatus = ConversationAssignmentStatus.Assigned;

            return Task.FromResult(true);
        }

        // Queue target ("department"). Phase 1 supports the shared-pool model (claim-to-own); Routed
        // reservation/assignment lands in a later phase, so an unrecognized mode falls back to the shared pool
        // rather than dropping the message.
        conversation.OwnerType = ConversationOwnerType.Queue;
        conversation.OwnerId = routing.TargetId;
        conversation.AssignedAgentId = null;
        conversation.AssignmentStatus = ConversationAssignmentStatus.Pooled;

        return Task.FromResult(true);
    }
}
