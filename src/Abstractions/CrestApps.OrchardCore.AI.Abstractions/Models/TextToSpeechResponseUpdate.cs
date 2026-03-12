namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Represents a streaming update from a text-to-speech synthesis operation.
/// Contains a chunk of audio data for incremental playback.
/// </summary>
public sealed class TextToSpeechResponseUpdate
{
    /// <summary>
    /// Gets or sets the audio data chunk.
    /// </summary>
    public byte[] AudioData { get; set; }

    /// <summary>
    /// Gets or sets the MIME content type of the audio (e.g., "audio/mp3", "audio/wav").
    /// </summary>
    public string ContentType { get; set; }
}
