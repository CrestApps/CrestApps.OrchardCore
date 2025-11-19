using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using OpenAI.Audio;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Services;

/// <summary>
/// Custom implementation of ISpeechToTextClient for Azure Whisper deployments.
/// Azure Whisper uses the /audio/transcriptions endpoint instead of the standard /audio/speech-to-text endpoint.
/// </summary>
#pragma warning disable MEAI001, OPENAI001, AOAI001 // Types/APIs are preview and subject to change
public sealed class AzureSpeechToTextClient : ISpeechToTextClient
{
    private readonly AudioClient _audioClient;

    public AzureSpeechToTextClient(AzureOpenAIClient client, string deploymentName)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrEmpty(deploymentName);

        _audioClient = client.GetAudioClient(deploymentName);
    }

    public AzureSpeechToTextClient(AudioClient audioClient)
    {
        ArgumentNullException.ThrowIfNull(audioClient);

        _audioClient = audioClient;
    }

    public async Task<SpeechToTextResponse> GetTextAsync(Stream audioSpeechStream, SpeechToTextOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audioSpeechStream);

        var transcriptionOptions = new AudioTranscriptionOptions
        {
            ResponseFormat = AudioTranscriptionFormat.Text,
        };

        if (!string.IsNullOrEmpty(options?.TextLanguage))
        {
            transcriptionOptions.Language = options.TextLanguage;
        }

        using var memoryStream = new MemoryStream();
        await audioSpeechStream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
        memoryStream.Position = 0;

        var result = await _audioClient
            .TranscribeAudioAsync(memoryStream, "audio.webm", transcriptionOptions, cancellationToken)
            .ConfigureAwait(false);

        return new SpeechToTextResponse(result.Value?.Text);
    }

    public async IAsyncEnumerable<SpeechToTextResponseUpdate> GetStreamingTextAsync(Stream audioSpeechStream, SpeechToTextOptions options, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var transcriptionOptions = new AudioTranscriptionOptions();

        if (!string.IsNullOrEmpty(options?.TextLanguage))
        {
            transcriptionOptions.Language = options.TextLanguage;
        }

        await foreach (var update in _audioClient.TranscribeAudioStreamingAsync(audioSpeechStream, "audio.webm", transcriptionOptions, cancellationToken).ConfigureAwait(false))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var responseId = Guid.NewGuid().ToString();

            if (update is StreamingAudioTranscriptionTextDoneUpdate doneUpdate)
            {
                yield return new SpeechToTextResponseUpdate(doneUpdate.Text)
                {
                    ResponseId = responseId,
                    Kind = SpeechToTextResponseUpdateKind.SessionClose,
                };
            }
            else if (update is StreamingAudioTranscriptionTextDeltaUpdate textUpdate)
            {
                yield return new SpeechToTextResponseUpdate(textUpdate.Delta)
                {
                    ResponseId = responseId,
                    Kind = SpeechToTextResponseUpdateKind.TextUpdating,
                };
            }

            yield return new SpeechToTextResponseUpdate
            {
                ResponseId = responseId,
                Contents = null,
            };
        }
    }

    public void Dispose()
    {
        // No resources to dispose
    }

    public object GetService(Type serviceType, object serviceKey = null)
    {
        if (serviceType == null)
        {
            return null;
        }

        if (serviceType.IsAssignableFrom(typeof(AudioClient)))
        {
            return _audioClient;
        }

        return null;
    }
}
#pragma warning restore MEAI001, OPENAI001, AOAI001
