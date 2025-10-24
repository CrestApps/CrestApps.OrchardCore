namespace CrestApps.OrchardCore.AI.Core.Models;

public class AIProfileMetadata
{
    public string SystemMessage { get; set; }

    public float? Temperature { get; set; }

    public float? TopP { get; set; }

    public float? FrequencyPenalty { get; set; }

    public float? PresencePenalty { get; set; }

    public int? MaxTokens { get; set; }

    public int? PastMessagesCount { get; set; }

    public bool UseCaching { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the microphone input is enabled for this profile.
    /// </summary>
    public bool UseMicrophone { get; set; }

    /// <summary>
    /// Gets or sets the connection name for speech-to-text when microphone is enabled.
    /// </summary>
    public string SpeechToTextConnectionName { get; set; }
}
