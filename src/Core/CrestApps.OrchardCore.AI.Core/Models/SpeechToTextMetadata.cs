namespace CrestApps.OrchardCore.AI.Core.Models;

public class SpeechToTextMetadata
{
    /// <summary>
    /// Gets or sets a value indicating whether the microphone input is enabled for this profile.
    /// </summary>
    public bool UseMicrophone { get; set; }

    /// <summary>
    /// Gets or sets the connection name for speech-to-text when microphone is enabled.
    /// </summary>
    public string ConnectionName { get; set; }
}
