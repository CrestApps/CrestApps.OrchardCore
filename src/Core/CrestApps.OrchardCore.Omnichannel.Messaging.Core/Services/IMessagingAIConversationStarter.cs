using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

/// <summary>
/// Starts an AI conversation for an inbound message when the entry point that answers its address routes it to an AI
/// agent, so the AI takes the customer's first message instead of a person.
/// </summary>
/// <remarks>
/// Both the messaging workspace and the automated conversation handle every inbound message, in no fixed order. Each
/// asks the starter first, and the starter starts a conversation once: the first to ask creates it, and the other finds
/// it. The workspace then leaves the message to the AI, as it does for any live automated conversation.
/// </remarks>
public interface IMessagingAIConversationStarter
{
    /// <summary>
    /// Starts an AI conversation for the message when its address is routed to an AI agent, or finds the one already
    /// started for it.
    /// </summary>
    /// <param name="message">The inbound message.</param>
    /// <param name="address">The business address the message was sent to.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when a live AI conversation owns the message; otherwise, <see langword="false"/>.</returns>
    Task<bool> TryStartAsync(OmnichannelMessage message, OmnichannelChannelEndpoint address, CancellationToken cancellationToken = default);
}
