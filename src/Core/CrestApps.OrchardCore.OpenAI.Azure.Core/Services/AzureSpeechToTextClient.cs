using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Services;

/// <summary>
/// Custom implementation of ISpeechToTextClient using Azure Cognitive Services Speech SDK.
/// This provides more reliable speech-to-text functionality for Azure compared to the Azure OpenAI Whisper endpoint.
/// </summary>
#pragma warning disable MEAI001 // Type is for evaluation purposes only and is subject to change
public sealed class AzureSpeechToTextClient : ISpeechToTextClient
{
    private readonly SpeechConfig _speechConfig;
    private readonly string _region;
    private readonly string _subscriptionKey;

    /// <summary>
    /// Initializes a new instance of the AzureSpeechToTextClient.
    /// </summary>
    /// <param name="region">The Azure region for the Speech service (e.g., "westus", "eastus")</param>
    /// <param name="subscriptionKey">The subscription key for the Azure Speech service</param>
    public AzureSpeechToTextClient(string region, string subscriptionKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(region);
        ArgumentException.ThrowIfNullOrEmpty(subscriptionKey);

        _region = region;
        _subscriptionKey = subscriptionKey;
        _speechConfig = SpeechConfig.FromSubscription(subscriptionKey, region);
    }

    public async Task<SpeechToTextResponse> GetTextAsync(Stream audioSpeechStream, SpeechToTextOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audioSpeechStream);

        // Configure language if specified
        if (!string.IsNullOrEmpty(options?.TextLanguage))
        {
            _speechConfig.SpeechRecognitionLanguage = options.TextLanguage;
        }
        else
        {
            // Default to English (United States)
            _speechConfig.SpeechRecognitionLanguage = "en-US";
        }

        // Copy stream to memory for Azure Speech SDK
        using var memoryStream = new MemoryStream();
        await audioSpeechStream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
        memoryStream.Position = 0;

        // Create audio format for WebM/Opus audio
        // Azure Speech SDK supports various formats; we'll use WAV for compatibility
        // Note: The client might need to convert WebM to WAV before sending
        var audioFormat = AudioStreamFormat.GetWaveFormatPCM(16000, 16, 1);
        
        using var audioInputStream = AudioInputStream.CreatePushStream(audioFormat);
        using var audioConfig = AudioConfig.FromStreamInput(audioInputStream);
        using var recognizer = new SpeechRecognizer(_speechConfig, audioConfig);

        // Write audio data to the push stream
        var buffer = memoryStream.ToArray();
        audioInputStream.Write(buffer);
        audioInputStream.Close();

        // Perform recognition
        var result = await recognizer.RecognizeOnceAsync().ConfigureAwait(false);

        return result.Reason switch
        {
            ResultReason.RecognizedSpeech => new SpeechToTextResponse(result.Text),
            ResultReason.NoMatch => new SpeechToTextResponse(string.Empty),
            ResultReason.Canceled => throw new InvalidOperationException($"Speech recognition canceled: {CancellationDetails.FromResult(result).ErrorDetails}"),
            _ => throw new InvalidOperationException($"Unexpected result reason: {result.Reason}")
        };
    }

    public async IAsyncEnumerable<SpeechToTextResponseUpdate> GetStreamingTextAsync(
        Stream audioSpeechStream,
        SpeechToTextOptions options,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audioSpeechStream);

        // Configure language if specified
        if (!string.IsNullOrEmpty(options?.TextLanguage))
        {
            _speechConfig.SpeechRecognitionLanguage = options.TextLanguage;
        }
        else
        {
            _speechConfig.SpeechRecognitionLanguage = "en-US";
        }

        var audioFormat = AudioStreamFormat.GetWaveFormatPCM(16000, 16, 1);
        using var audioInputStream = AudioInputStream.CreatePushStream(audioFormat);
        using var audioConfig = AudioConfig.FromStreamInput(audioInputStream);
        using var recognizer = new SpeechRecognizer(_speechConfig, audioConfig);

        var tcs = new TaskCompletionSource<bool>();
        var responseId = Guid.NewGuid().ToString();
        var recognizedTexts = new List<string>();

        // Subscribe to recognition events
        recognizer.Recognizing += (s, e) =>
        {
            if (e.Result.Reason == ResultReason.RecognizingSpeech && !string.IsNullOrEmpty(e.Result.Text))
            {
                // Intermediate results
                recognizedTexts.Add(e.Result.Text);
            }
        };

        recognizer.Recognized += (s, e) =>
        {
            if (e.Result.Reason == ResultReason.RecognizedSpeech && !string.IsNullOrEmpty(e.Result.Text))
            {
                // Final result for this utterance
                recognizedTexts.Add(e.Result.Text);
            }
        };

        recognizer.SessionStopped += (s, e) =>
        {
            tcs.TrySetResult(true);
        };

        recognizer.Canceled += (s, e) =>
        {
            if (e.Reason == CancellationReason.Error)
            {
                tcs.TrySetException(new InvalidOperationException($"Speech recognition error: {e.ErrorDetails}"));
            }
            else
            {
                tcs.TrySetResult(true);
            }
        };

        // Start continuous recognition
        await recognizer.StartContinuousRecognitionAsync().ConfigureAwait(false);

        try
        {
            // Stream audio data
            var buffer = new byte[4096];
            int bytesRead;
            while ((bytesRead = await audioSpeechStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
            {
                audioInputStream.Write(buffer, bytesRead);

                // Yield any recognized text
                while (recognizedTexts.Count > 0)
                {
                    var text = recognizedTexts[0];
                    recognizedTexts.RemoveAt(0);

                    yield return new SpeechToTextResponseUpdate(text)
                    {
                        ResponseId = responseId,
                        Kind = SpeechToTextResponseUpdateKind.TextUpdating,
                    };
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }

            audioInputStream.Close();
            await tcs.Task.ConfigureAwait(false);

            // Yield any remaining recognized text
            while (recognizedTexts.Count > 0)
            {
                var text = recognizedTexts[0];
                recognizedTexts.RemoveAt(0);

                yield return new SpeechToTextResponseUpdate(text)
                {
                    ResponseId = responseId,
                    Kind = SpeechToTextResponseUpdateKind.TextUpdating,
                };
            }

            // Signal completion
            yield return new SpeechToTextResponseUpdate
            {
                ResponseId = responseId,
                Kind = SpeechToTextResponseUpdateKind.SessionClose,
            };
        }
        finally
        {
            await recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        _speechConfig?.Dispose();
    }

    public object GetService(Type serviceType, object serviceKey = null)
    {
        if (serviceType == null)
        {
            return null;
        }

        if (serviceType.IsAssignableFrom(typeof(SpeechConfig)))
        {
            return _speechConfig;
        }

        return null;
    }
}
#pragma warning restore MEAI001
