using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Runs the <c>AI Conversation Starting</c> handlers for an automated activity before its first message, and keeps
/// what they decided on the activity: the AI profile and the variables the conversation starts with.
/// </summary>
public interface IOmnichannelAIConversationStarting
{
    /// <summary>
    /// Runs the starting handlers for the activity's conversation. The caller saves the activity.
    /// </summary>
    /// <param name="activity">The activity whose conversation is about to start.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>What the handlers decided. When <see cref="AIConversationStartingResult.Declined"/> is set the
    /// conversation must not take place.</returns>
    Task<AIConversationStartingResult> StartingAsync(OmnichannelActivity activity, CancellationToken cancellationToken = default);
}
