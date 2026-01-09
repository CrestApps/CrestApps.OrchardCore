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
    private enum AudioFormat
    {
        Unknown,
        Wav,
        Mp3,
        OggOpus,
        Flac,
        Alaw,
        Mulaw,
        AmrNb,
        AmrWb,
        WebM,
    }

    private const string DefaultLanguage = "en-US";
    private const uint DefaultSamplesPerSecond = 16000;
    private const byte DefaultBitsPerSample = 16;
    private const byte DefaultChannels = 1;

    private readonly SpeechConfig _speechConfig;

    /// <summary>
    /// Initializes a new instance of the AzureSpeechToTextClient.
    /// </summary>
    /// <param name="region">The Azure region for the Speech service (e.g., "westus", "eastus")</param>
    /// <param name="subscriptionKey">The subscription key for the Azure Speech service</param>
    public AzureSpeechToTextClient(string region, string subscriptionKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(region);
        ArgumentException.ThrowIfNullOrEmpty(subscriptionKey);

        _speechConfig = SpeechConfig.FromSubscription(subscriptionKey, region);
    }

    public async Task<SpeechToTextResponse> GetTextAsync(Stream audioSpeechStream, SpeechToTextOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audioSpeechStream);

        if (audioSpeechStream.Length == 0)
        {
            return new SpeechToTextResponse(string.Empty);
        }

        // Configure language if specified
        if (!string.IsNullOrEmpty(options?.TextLanguage))
        {
            _speechConfig.SpeechRecognitionLanguage = options.TextLanguage;
        }
        else
        {
            _speechConfig.SpeechRecognitionLanguage = DefaultLanguage;
        }

        // Copy stream to memory for Azure Speech SDK
        using var memoryStream = new MemoryStream();
        await audioSpeechStream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
        memoryStream.Position = 0;

        // Detect audio format from stream header
        var detectedFormat = DetectAudioFormat(memoryStream);
        memoryStream.Position = 0;

        // Create appropriate audio format based on detection
        var audioFormat = CreateAudioStreamFormat(detectedFormat);

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

        if (audioSpeechStream.Length == 0)
        {
            yield return new SpeechToTextResponseUpdate
            {
                ResponseId = Guid.NewGuid().ToString(),
                Kind = SpeechToTextResponseUpdateKind.SessionClose,
            };
            yield break;
        }

        // Configure language if specified
        if (!string.IsNullOrEmpty(options?.TextLanguage))
        {
            _speechConfig.SpeechRecognitionLanguage = options.TextLanguage;
        }
        else
        {
            _speechConfig.SpeechRecognitionLanguage = DefaultLanguage;
        }

        // Seek to beginning if possible
        if (audioSpeechStream.CanSeek)
        {
            audioSpeechStream.Seek(0, SeekOrigin.Begin);
        }

        // Detect audio format if stream is seekable
        AudioFormat detectedFormat = AudioFormat.Unknown;
        if (audioSpeechStream.CanSeek)
        {
            detectedFormat = DetectAudioFormat(audioSpeechStream);
            audioSpeechStream.Position = 0;
        }

        // Create appropriate audio format based on detection or default to PCM WAV
        var audioFormat = detectedFormat != AudioFormat.Unknown
            ? CreateAudioStreamFormat(detectedFormat)
            : AudioStreamFormat.GetWaveFormatPCM(DefaultSamplesPerSecond, DefaultBitsPerSample, DefaultChannels);

        using var audioInputStream = AudioInputStream.CreatePushStream(audioFormat);
        using var audioConfig = AudioConfig.FromStreamInput(audioInputStream);
        using var recognizer = new SpeechRecognizer(_speechConfig, audioConfig);

        var responseId = Guid.NewGuid().ToString();
        var recognitionComplete = new TaskCompletionSource<bool>();
        var combinedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var recognizedText = new System.Text.StringBuilder();

        // Subscribe to recognition events
        recognizer.Recognized += (s, e) =>
        {
            if (e.Result.Reason == ResultReason.RecognizedSpeech && !string.IsNullOrEmpty(e.Result.Text))
            {
                // Accumulate final result for this utterance
                recognizedText.Append(e.Result.Text);
                recognizedText.Append(' ');
            }
        };

        recognizer.SessionStopped += (s, e) =>
        {
            combinedCancellation.Cancel(); // Signal to stop the infinite wait
            recognitionComplete.TrySetResult(true);
        };

        recognizer.Canceled += (s, e) =>
        {
            combinedCancellation.Cancel(); // Signal to stop the infinite wait
            if (e.Reason == CancellationReason.Error)
            {
                recognitionComplete.TrySetException(new InvalidOperationException($"Speech recognition error: {e.ErrorDetails}"));
            }
            else
            {
                recognitionComplete.TrySetResult(true);
            }
        };

        // Start continuous recognition
        await recognizer.StartContinuousRecognitionAsync().ConfigureAwait(false);

        try
        {
            // Stream audio data
            var buffer = new byte[4096];
            int bytesRead;
            while ((bytesRead = await audioSpeechStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
            {
                audioInputStream.Write(buffer, bytesRead);

                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }

            // Close the audio stream to signal end of input
            audioInputStream.Close();

            // Wait for recognition to complete - this is critical!
            // Without this wait, the method returns before recognition finishes
            // The infinite delay will be cancelled by the SessionStopped/Canceled event handlers
            await Task.Delay(Timeout.Infinite, combinedCancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected when recognition completes or is cancelled via combinedCancellation
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error during speech recognition: {ex.Message}", ex);
        }
        finally
        {
            await recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);
            combinedCancellation.Dispose();
        }

        // Wait for recognition to complete
        await recognitionComplete.Task.ConfigureAwait(false);

        // Yield the accumulated recognized text
        var finalText = recognizedText.ToString().Trim();
        if (!string.IsNullOrEmpty(finalText))
        {
            yield return new SpeechToTextResponseUpdate(finalText)
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

    public void Dispose()
    {
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

    /// <summary>
    /// Detects the audio format from the stream header.
    /// </summary>
    private static AudioFormat DetectAudioFormat(Stream stream)
    {
        if (stream == null || !stream.CanSeek || stream.Length < 12)
        {
            return AudioFormat.Unknown;
        }

        var originalPosition = stream.Position;
        stream.Position = 0;

        try
        {
            var header = new byte[12];
            var bytesRead = stream.Read(header, 0, 12);

            if (bytesRead < 4)
            {
                return AudioFormat.Unknown;
            }

            // Check for WAV (RIFF)
            if (header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46)
            {
                return AudioFormat.Wav;
            }

            // Check for MP3
            if ((header[0] == 0xFF && (header[1] & 0xE0) == 0xE0) || // MPEG sync
                (header[0] == 0x49 && header[1] == 0x44 && header[2] == 0x33)) // ID3
            {
                return AudioFormat.Mp3;
            }

            // Check for Ogg Opus
            if (header[0] == 0x4F && header[1] == 0x67 && header[2] == 0x67 && header[3] == 0x53)
            {
                return AudioFormat.OggOpus;
            }

            // Check for FLAC
            if (header[0] == 0x66 && header[1] == 0x4C && header[2] == 0x61 && header[3] == 0x43)
            {
                return AudioFormat.Flac;
            }

            // Check for WebM (EBML header)
            if (bytesRead >= 4 && header[0] == 0x1A && header[1] == 0x45 && header[2] == 0xDF && header[3] == 0xA3)
            {
                return AudioFormat.WebM;
            }

            // Check for AMR-NB
            if (bytesRead >= 6 && header[0] == 0x23 && header[1] == 0x21 && header[2] == 0x41 &&
                header[3] == 0x4D && header[4] == 0x52 && header[5] == 0x0A)
            {
                return AudioFormat.AmrNb;
            }

            // Check for AMR-WB
            if (bytesRead >= 9 && header[0] == 0x23 && header[1] == 0x21 && header[2] == 0x41 &&
                header[3] == 0x4D && header[4] == 0x52 && header[5] == 0x2D && header[6] == 0x57 &&
                header[7] == 0x42 && header[8] == 0x0A)
            {
                return AudioFormat.AmrWb;
            }

            return AudioFormat.Unknown;
        }
        finally
        {
            stream.Position = originalPosition;
        }
    }

    /// <summary>
    /// Creates an appropriate AudioStreamFormat based on the detected audio format.
    /// </summary>
    private static AudioStreamFormat CreateAudioStreamFormat(AudioFormat format)
    {
        return format switch
        {
            AudioFormat.Wav => AudioStreamFormat.GetWaveFormatPCM(DefaultSamplesPerSecond, DefaultBitsPerSample, DefaultChannels),
            AudioFormat.Mp3 => AudioStreamFormat.GetCompressedFormat(AudioStreamContainerFormat.MP3),
            AudioFormat.OggOpus => AudioStreamFormat.GetCompressedFormat(AudioStreamContainerFormat.OGG_OPUS),
            AudioFormat.Flac => AudioStreamFormat.GetCompressedFormat(AudioStreamContainerFormat.FLAC),
            AudioFormat.Alaw => AudioStreamFormat.GetCompressedFormat(AudioStreamContainerFormat.ALAW),
            AudioFormat.Mulaw => AudioStreamFormat.GetCompressedFormat(AudioStreamContainerFormat.MULAW),
            AudioFormat.AmrNb => AudioStreamFormat.GetCompressedFormat(AudioStreamContainerFormat.AMRNB),
            AudioFormat.AmrWb => AudioStreamFormat.GetCompressedFormat(AudioStreamContainerFormat.AMRWB),
            AudioFormat.WebM => AudioStreamFormat.GetCompressedFormat(AudioStreamContainerFormat.OGG_OPUS), // WebM often uses Opus codec
            _ => AudioStreamFormat.GetWaveFormatPCM(DefaultSamplesPerSecond, DefaultBitsPerSample, DefaultChannels) // Default fallback
        };
    }
}
#pragma warning restore MEAI001
