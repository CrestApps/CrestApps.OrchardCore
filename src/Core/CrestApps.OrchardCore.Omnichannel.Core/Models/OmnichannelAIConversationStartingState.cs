namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Kept on an automated activity once its <c>AI Conversation Starting</c> handlers have run and let the conversation
/// go ahead, so a start that is held back (the sending address is at its limit) or retried after a failed attempt does
/// not run them again.
/// </summary>
internal sealed class OmnichannelAIConversationStartingState
{
    /// <summary>
    /// Gets or sets a value indicating whether the handlers ran and let the conversation go ahead.
    /// </summary>
    public bool Completed { get; set; }
}
