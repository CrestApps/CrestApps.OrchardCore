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
        WebM
    }

    private const string DefaultLanguage = "en-US";
    private const uint DefaultSamplesPerSecond = 16000;
    private const byte DefaultBitsPerSample = 16;
    private const byte DefaultChannels = 1;

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

        // Configure language if specified
        if (!string.IsNullOrEmpty(options?.TextLanguage))
        {
            _speechConfig.SpeechRecognitionLanguage = options.TextLanguage;
        }
        else
        {
            _speechConfig.SpeechRecognitionLanguage = DefaultLanguage;
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
