using CrestApps.Core.AI.Models;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Settings;

/// <summary>
/// Site-level settings that control the chat mode for chat interactions UI.
/// </summary>
public sealed class ChatInteractionChatModeSettings
{
    /// <summary>
    /// Gets or sets the chat mode for all chat interactions.
    /// Defaults to <see cref="ChatMode.TextInput"/>.
    /// </summary>
    public ChatMode ChatMode { get; set; }

    /// <summary>
    /// Gets or sets whether to show text-to-speech playback controls on
    /// assistant messages in chat interactions. When enabled the UI displays
    /// a play button on each assistant message, allowing the user to listen
    /// to the response via the configured TTS deployment.
    /// </summary>
    public bool EnableTextToSpeechPlayback { get; set; }
}
