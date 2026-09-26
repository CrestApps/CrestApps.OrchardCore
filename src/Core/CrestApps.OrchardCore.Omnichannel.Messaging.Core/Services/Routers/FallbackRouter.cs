using CrestApps.OrchardCore.Omnichannel.Messaging.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services.Routers;

/// <summary>
/// The terminal router: a message on a DID that no <c>SmsNumberRoute</c> claims lands in the unassigned inbox
/// rather than being silently dropped, so a supervisor can triage it. This is what replaces the old
/// "Unable to link incoming message… to an Activity" drop.
/// </summary>
public sealed class FallbackRouter : IMessagingInboundRouter
{
    /// <inheritdoc/>
    public int Order => 1000;

    /// <inheritdoc/>
    public Task<bool> TryRouteAsync(MessagingRoutingContext context, CancellationToken cancellationToken = default)
    {
        var conversation = context.Conversation;

        conversation.AssignmentStatus = ConversationAssignmentStatus.Unassigned;
        conversation.OwnerType = ConversationOwnerType.Personal;
        conversation.OwnerId = null;
        conversation.AssignedAgentId = null;

        return Task.FromResult(true);
    }
}
