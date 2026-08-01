namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Represents the effective AI and speech settings for an automated phone activity.
/// </summary>
public sealed class AutomatedVoiceActivitySettings
{
    /// <summary>
    /// Gets or sets the AI chat profile identifier.
    /// </summary>
    public string AIProfileId { get; set; }

    /// <summary>
    /// Gets or sets the effective speech-to-text deployment name.
    /// </summary>
    public string SpeechToTextDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the effective text-to-speech deployment name.
    /// </summary>
    public string TextToSpeechDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the effective text-to-speech voice identifier.
    /// </summary>
    public string TextToSpeechVoiceId { get; set; }
}
