using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Defines a client for converting text to speech audio.
/// </summary>
public interface ITextToSpeechClient : IDisposable
{
    /// <summary>
    /// Asynchronously converts the specified text to audio.
    /// </summary>
    /// <param name="text">The text to synthesize into speech.</param>
    /// <param name="options">Optional synthesis options such as voice, language, and output format.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="TextToSpeechResponse"/> containing the synthesized audio data.</returns>
    Task<TextToSpeechResponse> GetAudioAsync(
        string text,
        TextToSpeechOptions options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously converts the specified text to audio, streaming audio chunks as they become available.
    /// </summary>
    /// <param name="text">The text to synthesize into speech.</param>
    /// <param name="options">Optional synthesis options such as voice, language, and output format.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An async enumerable of <see cref="TextToSpeechResponseUpdate"/> containing audio chunks.</returns>
    IAsyncEnumerable<TextToSpeechResponseUpdate> GetStreamingAudioAsync(
        string text,
        TextToSpeechOptions options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the available voices for text-to-speech synthesis.
    /// </summary>
    /// <param name="locale">An optional locale to filter voices (e.g., "en-US"). If <c>null</c>, all voices are returned.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An array of available <see cref="SpeechVoice"/> instances.</returns>
    Task<SpeechVoice[]> GetVoicesAsync(
        string locale = null,
        CancellationToken cancellationToken = default);
}
