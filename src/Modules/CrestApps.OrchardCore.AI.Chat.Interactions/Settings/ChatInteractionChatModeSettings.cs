using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Settings;

/// <summary>
/// Site-level settings that control the chat mode for chat interactions UI.
/// </summary>
public sealed class ChatInteractionChatModeSettings
{
    /// <summary>
    /// Gets or sets the chat mode for all chat interactions.
    /// Defaults to <see cref="ChatMode.TextOnly"/>.
    /// </summary>
    public ChatMode ChatMode { get; set; }
}
