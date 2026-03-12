namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Options for text-to-speech synthesis.
/// </summary>
public sealed class TextToSpeechOptions
{
    /// <summary>
    /// Gets or sets the voice name to use for synthesis (e.g., "en-US-JennyNeural").
    /// When <c>null</c>, the provider's default voice is used.
    /// </summary>
    public string VoiceName { get; set; }

    /// <summary>
    /// Gets or sets the language for synthesis (e.g., "en-US").
    /// </summary>
    public string Language { get; set; }

    /// <summary>
    /// Gets or sets the desired output audio format (e.g., "audio-24khz-48kbitrate-mono-mp3").
    /// When <c>null</c>, the provider's default format is used.
    /// </summary>
    public string OutputFormat { get; set; }

    /// <summary>
    /// Gets or sets additional properties for provider-specific configuration.
    /// </summary>
    public IDictionary<string, object> AdditionalProperties { get; set; }
}
