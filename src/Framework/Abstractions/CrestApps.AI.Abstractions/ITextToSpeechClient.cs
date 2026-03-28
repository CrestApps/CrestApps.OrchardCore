using CrestApps.AI.Models;

namespace CrestApps.AI;

/// <summary>
/// Represents a text to speech client.
/// </summary>
/// <remarks>
/// <para>
/// Unless otherwise specified, all members of <see cref="ITextToSpeechClient"/> are thread-safe for concurrent use.
/// It is expected that all implementations of <see cref="ITextToSpeechClient"/> support being used by multiple requests concurrently.
/// </para>
/// <para>
/// However, implementations of <see cref="ITextToSpeechClient"/> might mutate the arguments supplied to
/// <see cref="GetAudioAsync"/> and <see cref="GetStreamingAudioAsync"/>, such as by configuring the options instance.
/// Thus, consumers of the interface either should avoid using shared instances of these arguments for concurrent
/// invocations or should otherwise ensure by construction that no <see cref="ITextToSpeechClient"/> instances are
/// used which might employ such mutation.
/// </para>
/// </remarks>
public interface ITextToSpeechClient : IDisposable
{
    /// <summary>
    /// Sends text content to the model and returns the generated audio speech.
    /// </summary>
    /// <param name="text">The text to synthesize into speech.</param>
    /// <param name="options">The text to speech options to configure the request.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The audio speech generated.</returns>
    Task<TextToSpeechResponse> GetAudioAsync(
        string text,
        TextToSpeechOptions options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends text content to the model and streams back the generated audio speech.
    /// </summary>
    /// <param name="text">The text to synthesize into speech.</param>
    /// <param name="options">The text to speech options to configure the request.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The audio speech updates representing the streamed output.</returns>
    IAsyncEnumerable<TextToSpeechResponseUpdate> GetStreamingAudioAsync(
        string text,
        TextToSpeechOptions options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks the <see cref="ITextToSpeechClient"/> for an object of the specified type <paramref name="serviceType"/>.
    /// </summary>
    /// <param name="serviceType">The type of object being requested.</param>
    /// <param name="serviceKey">An optional key that can be used to help identify the target service.</param>
    /// <returns>The found object, otherwise <see langword="null"/>.</returns>
    object GetService(Type serviceType, object serviceKey = null);
}
