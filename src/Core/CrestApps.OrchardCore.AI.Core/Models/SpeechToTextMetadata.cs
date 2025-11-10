namespace CrestApps.OrchardCore.AI.Core.Models;

public sealed class SpeechToTextMetadata
{
    /// <summary>
    /// Gets or sets a value indicating whether the microphone input is enabled for this profile.
    /// </summary>
    public bool UseMicrophone { get; set; }

    /// <summary>
    /// Gets or sets the connection name for speech-to-text when microphone is enabled.
    /// </summary>
    public string ConnectionName { get; set; }

    /// <summary>
    /// Gets or sets the provider name for speech-to-text connection.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the deployment ID for speech-to-text when microphone is enabled.
    /// </summary>
    public string DeploymentId { get; set; }
}
