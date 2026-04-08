using CrestApps.Core.AI.ResponseHandling;

namespace CrestApps.Core.AI.Models;

/// <summary>
/// Identifies the type of chat context a <see cref="ChatResponseHandlerContext"/> represents.
/// </summary>
public enum ChatContextType
{
    /// <summary>
    /// The context is for an <see cref="AIChatSession"/> managed by the AI Chat module.
    /// </summary>
    AIChatSession,

    /// <summary>
    /// The context is for a <see cref="ChatInteraction"/> managed by the Chat Interactions module.
    /// </summary>
    ChatInteraction,
}
