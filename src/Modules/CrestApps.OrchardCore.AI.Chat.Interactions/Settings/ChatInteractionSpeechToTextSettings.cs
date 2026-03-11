namespace CrestApps.OrchardCore.AI.Chat.Interactions.Settings;

/// <summary>
/// Site-level settings that control whether speech-to-text input
/// is available in the chat interactions UI.
/// </summary>
public sealed class ChatInteractionSpeechToTextSettings
{
    /// <summary>
    /// Gets or sets whether speech-to-text input via microphone is enabled
    /// for all chat interactions.
    /// </summary>
    public bool EnableSpeechToText { get; set; }
}
