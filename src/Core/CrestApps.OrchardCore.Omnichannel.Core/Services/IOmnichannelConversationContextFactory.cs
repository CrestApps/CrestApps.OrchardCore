using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Describes an automated omnichannel conversation (an AI phone call or text thread) as an
/// <see cref="AIConversationContext"/>, so the system message, tools and workflows see the same values on every
/// channel.
/// </summary>
public interface IOmnichannelConversationContextFactory
{
    /// <summary>
    /// Creates the context of the conversation an activity's AI is having.
    /// </summary>
    /// <param name="activity">The activity the conversation belongs to.</param>
    /// <param name="session">The AI session the conversation is stored in, when it exists yet.</param>
    /// <param name="profile">The AI profile answering, when it is known.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The conversation's context.</returns>
    Task<AIConversationContext> CreateAsync(
        OmnichannelActivity activity,
        AIChatSession session,
        AIProfile profile,
        CancellationToken cancellationToken = default);
}
