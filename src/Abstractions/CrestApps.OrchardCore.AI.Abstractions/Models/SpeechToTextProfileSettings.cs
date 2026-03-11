namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Settings stored on <see cref="AIProfile.Settings"/> to control
/// whether speech-to-text input is enabled for chat UIs using this profile.
/// </summary>
public class SpeechToTextProfileSettings
{
    /// <summary>
    /// Gets or sets whether speech-to-text input via microphone is enabled
    /// for chat sessions that use this profile.
    /// </summary>
    public bool EnableSpeechToText { get; set; }
}
