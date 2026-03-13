using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Represents the options for a text to speech request.
/// </summary>
public class TextToSpeechOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TextToSpeechOptions"/> class.
    /// </summary>
    public TextToSpeechOptions()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TextToSpeechOptions"/> class,
    /// performing a shallow copy of all properties from <paramref name="other"/>.
    /// </summary>
    protected TextToSpeechOptions(TextToSpeechOptions other)
    {
        if (other is null)
        {
            return;
        }

        AdditionalProperties = other.AdditionalProperties is not null
            ? new Dictionary<string, object>(other.AdditionalProperties)
            : null;
        AudioFormat = other.AudioFormat;
        Language = other.Language;
        ModelId = other.ModelId;
        Pitch = other.Pitch;
        RawRepresentationFactory = other.RawRepresentationFactory;
        Speed = other.Speed;
        VoiceId = other.VoiceId;
        Volume = other.Volume;
    }

    /// <summary>
    /// Gets or sets the model ID for the text to speech request.
    /// </summary>
    public string ModelId { get; set; }

    /// <summary>
    /// Gets or sets the voice identifier to use for speech synthesis.
    /// </summary>
    public string VoiceId { get; set; }

    /// <summary>
    /// Gets or sets the language for the generated speech.
    /// </summary>
    /// <remarks>
    /// This is typically a BCP 47 language tag (e.g., "en-US", "fr-FR").
    /// </remarks>
    public string Language { get; set; }

    /// <summary>
    /// Gets or sets the desired audio output format.
    /// </summary>
    /// <remarks>
    /// This may be a media type (e.g., "audio/mpeg") or a provider-specific format name (e.g., "mp3", "wav", "opus").
    /// When not specified, the provider's default format is used.
    /// </remarks>
    public string AudioFormat { get; set; }

    /// <summary>
    /// Gets or sets the speech speed multiplier.
    /// </summary>
    /// <remarks>
    /// A value of 1.0 represents normal speed. Values greater than 1.0 increase speed;
    /// values less than 1.0 decrease speed. The valid range is provider-specific.
    /// </remarks>
    public float? Speed { get; set; }

    /// <summary>
    /// Gets or sets the speech pitch multiplier.
    /// </summary>
    /// <remarks>
    /// A value of 1.0 represents normal pitch. Values greater than 1.0 increase pitch;
    /// values less than 1.0 decrease pitch. The valid range is provider-specific.
    /// </remarks>
    public float? Pitch { get; set; }

    /// <summary>
    /// Gets or sets the speech volume level.
    /// </summary>
    /// <remarks>
    /// The valid range and interpretation is provider-specific; a common convention is
    /// 0.0 (silent) to 1.0 (full volume).
    /// </remarks>
    public float? Volume { get; set; }

    /// <summary>
    /// Gets or sets any additional properties associated with the options.
    /// </summary>
    public IDictionary<string, object> AdditionalProperties { get; set; }

    /// <summary>
    /// Gets or sets a callback responsible for creating the raw representation of the
    /// text to speech options from an underlying implementation.
    /// </summary>
    [JsonIgnore]
    public Func<ITextToSpeechClient, object> RawRepresentationFactory { get; set; }

    /// <summary>
    /// Produces a clone of the current <see cref="TextToSpeechOptions"/> instance.
    /// </summary>
    /// <returns>A clone of the current <see cref="TextToSpeechOptions"/> instance.</returns>
    public virtual TextToSpeechOptions Clone() => new(this);
}
