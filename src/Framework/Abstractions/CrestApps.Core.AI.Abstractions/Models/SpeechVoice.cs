namespace CrestApps.Core.AI.Models;

/// <summary>
/// Represents an available voice for text-to-speech synthesis.
/// </summary>
public sealed class SpeechVoice
{
    /// <summary>
    /// Gets or sets the unique identifier of the voice (e.g., "en-US-JennyNeural").
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the display name of the voice (e.g., "Jenny").
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the language/locale of this voice (e.g., "en-US").
    /// </summary>
    public string Language { get; set; }

    /// <summary>
    /// Gets or sets the URL to a sample audio clip of this voice.
    /// </summary>
    public string VoiceSampleUrl { get; set; }

    /// <summary>
    /// Gets or sets the gender of this voice.
    /// </summary>
    public SpeechVoiceGender Gender { get; set; }
}
