using System.Runtime.CompilerServices;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using OpenAI.Audio;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Services;

/// <summary>
/// Custom implementation of ISpeechToTextClient for Azure Whisper deployments.
/// Azure Whisper uses the /audio/transcriptions endpoint instead of the standard /audio/speech-to-text endpoint.
/// </summary>
#pragma warning disable MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
public sealed class AzureWhisperSpeechToTextClient : ISpeechToTextClient
#pragma warning restore MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
{
    private readonly AudioClient _audioClient;

    public AzureWhisperSpeechToTextClient(AzureOpenAIClient client, string deploymentName)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(deploymentName);

        _audioClient = client.GetAudioClient(deploymentName);
    }

#pragma warning disable MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    public async Task<string> GetTextAsync(
        Stream audio,
        SpeechToTextOptions? options = null,
        CancellationToken cancellationToken = default)
#pragma warning restore MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    {
        ArgumentNullException.ThrowIfNull(audio);

        // Use Azure's transcription API to get the full text
        var transcriptionOptions = new AudioTranscriptionOptions
        {
            ResponseFormat = AudioTranscriptionFormat.Text
        };

        // Set language if provided in options
        if (!string.IsNullOrEmpty(options?.Language))
        {
            transcriptionOptions.Language = options.Language;
        }

        // Create a BinaryData from the stream for Azure API
        using var memoryStream = new MemoryStream();
        await audio.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
        memoryStream.Position = 0;
        
        var result = await _audioClient.TranscribeAudioAsync(
            memoryStream,
            "audio.webm",
            transcriptionOptions,
            cancellationToken).ConfigureAwait(false);

        return result.Value.Text;
    }

    // Azure Whisper does not support streaming transcription yet.
    // We emulate streaming by returning the final transcription as a single update.
#pragma warning disable MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    public async IAsyncEnumerable<SpeechToTextUpdate> GetStreamingTextAsync(
        Stream audio,
        SpeechToTextOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
#pragma warning restore MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    {
        var text = await GetTextAsync(audio, options, cancellationToken).ConfigureAwait(false);
        yield return new SpeechToTextUpdate { Text = text, Final = true };
    }

    public void Dispose()
    {
        // No resources to dispose
    }
}
