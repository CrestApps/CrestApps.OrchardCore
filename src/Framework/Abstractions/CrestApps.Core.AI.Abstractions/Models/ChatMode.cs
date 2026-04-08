namespace CrestApps.Core.AI.Models;

/// <summary>
/// Defines the chat input/output mode for an AI chat profile or interaction.
/// Controls whether voice features (microphone, text-to-speech) are available.
/// </summary>
public enum ChatMode
{
    /// <summary>
    /// Standard text-only chat. No voice features are enabled.
    /// </summary>
    TextInput,

    /// <summary>
    /// Audio input mode. A microphone button is shown so users can
    /// dictate their prompts via speech-to-text. The user must still
    /// manually send the transcribed message.
    /// Requires a default speech-to-text deployment to be configured.
    /// </summary>
    AudioInput,

    /// <summary>
    /// Full conversation mode with two-way voice interaction.
    /// The user speaks, the transcript is sent directly as a prompt,
    /// the AI response is spoken back, and recording restarts automatically.
    /// Requires both speech-to-text and text-to-speech deployments.
    /// </summary>
    Conversation,
}
