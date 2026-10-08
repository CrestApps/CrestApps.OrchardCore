using CrestApps.OrchardCore.Omnichannel.Messaging.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services.Routers;

/// <summary>
/// Keeps an inbound message flowing into the conversation it already belongs to, preserving the existing
/// assignment. It runs before the number-route router so, after an AI-to-human handoff, replies land in the
/// human thread instead of re-triggering ownership resolution.
/// </summary>
public sealed class ExistingConversationRouter : IMessagingInboundRouter
{
    /// <inheritdoc/>
    public int Order => 200;

    /// <inheritdoc/>
    public Task<bool> TryRouteAsync(MessagingRoutingContext context, CancellationToken cancellationToken = default)
    {
        if (context.IsNewConversation)
        {
            return Task.FromResult(false);
        }

        // Re-opening a closed thread the contact texted again: bring it back into the inbox but keep its owner.
        if (context.Conversation.Status is ConversationStatus.Closed or ConversationStatus.Snoozed)
        {
            context.Conversation.Status = ConversationStatus.Open;
        }

        return Task.FromResult(true);
    }
}
