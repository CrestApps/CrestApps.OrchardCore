using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
    }

    private const string DefaultLanguage = "en-US";

    private readonly SpeechConfig _speechConfig;
    private readonly ILogger _logger;

    public AzureSpeechToTextClient(string region, string subscriptionKey, ILogger logger)
    {
        ArgumentException.ThrowIfNullOrEmpty(region);
        ArgumentException.ThrowIfNullOrEmpty(subscriptionKey);

        _speechConfig = SpeechConfig.FromSubscription(subscriptionKey, region);
        _logger = logger ?? NullLogger<AzureSpeechToTextClient>.Instance;

        _logger.LogDebug("AzureSpeechToTextClient initialized. Region={Region}", region);
    }

    public async Task<SpeechToTextResponse> GetTextAsync(Stream audioSpeechStream, SpeechToTextOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audioSpeechStream);

        _logger.LogDebug("GetTextAsync called. StreamCanSeek={CanSeek} Length={Length}", audioSpeechStream.CanSeek, audioSpeechStream.CanSeek ? audioSpeechStream.Length.ToString() : "<unknown>");

        if (audioSpeechStream.CanSeek && audioSpeechStream.Length == 0)
        {
            _logger.LogDebug("GetTextAsync: stream length is 0; returning empty response");
            return new SpeechToTextResponse(string.Empty);
        }

        // Use the stream directly if seekable, otherwise copy to a MemoryStream
        MemoryStream ownedMemoryStream = null;
        Stream workingStream;

        if (audioSpeechStream.CanSeek)
        {
            audioSpeechStream.Seek(0, SeekOrigin.Begin);
            workingStream = audioSpeechStream;
            _logger.LogDebug("Using input stream directly. Length={Length}", audioSpeechStream.Length);
        }
        else
        {
            ownedMemoryStream = new MemoryStream();
            await audioSpeechStream.CopyToAsync(ownedMemoryStream, cancellationToken).ConfigureAwait(false);
            ownedMemoryStream.Position = 0;
            workingStream = ownedMemoryStream;
            _logger.LogDebug("Copied audio to memory stream. Bytes={Length}", ownedMemoryStream.Length);

            if (ownedMemoryStream.Length == 0)
            {
                _logger.LogWarning("GetTextAsync: copied stream has 0 bytes; returning empty response");
                ownedMemoryStream.Dispose();
                return new SpeechToTextResponse(string.Empty);
            }
        }

        try
        {
            // Configure language if specified
            if (!string.IsNullOrEmpty(options?.TextLanguage))
            {
                _speechConfig.SpeechRecognitionLanguage = options.TextLanguage;
                _logger.LogDebug("Set speech recognition language to '{Language}'", options.TextLanguage);
            }
            else
            {
                _speechConfig.SpeechRecognitionLanguage = DefaultLanguage;
                _logger.LogDebug("Using default speech recognition language '{Language}'", DefaultLanguage);
            }

            // Detect audio format from stream header
            var detectedFormat = DetectAudioFormat(workingStream);
            workingStream.Position = 0;

            _logger.LogDebug("Detected audio format: {Format}", detectedFormat);

            if (detectedFormat == AudioFormat.Unknown)
            {
                _logger.LogWarning("Unknown audio format detected, cannot transcribe");
                return new SpeechToTextResponse(string.Empty);
            }

            // Create appropriate audio format based on detection
            using var audioFormat = CreateAudioStreamFormat(detectedFormat);
            using var audioInputStream = AudioInputStream.CreatePullStream(new AudioStreamReader(workingStream), audioFormat);
            using var audioConfig = AudioConfig.FromStreamInput(audioInputStream);
            using var recognizer = new SpeechRecognizer(_speechConfig, audioConfig);


            using var manualCancellationTokenSource = new CancellationTokenSource();
            using var combinedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, manualCancellationTokenSource.Token);
            var eventHandler = new RecognizerEventHandler(_logger, manualCancellationTokenSource);

            recognizer.Recognized += eventHandler.Recognized;
            recognizer.Canceled += eventHandler.Canceled;
            recognizer.SessionStopped += eventHandler.Stopped;

            try
            {
                _logger.LogDebug("Starting continuous recognition");
                await recognizer.StartContinuousRecognitionAsync().ConfigureAwait(false);

                // Wait for recognition to complete - cancelled by SessionStopped or Canceled events
                await Task.Delay(Timeout.Infinite, combinedCancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Recognition was canceled by the caller");
            }
            catch (OperationCanceledException)
            {
                // Expected when recognition completes normally
                _logger.LogDebug("Recognition completed normally");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during speech recognition");
            }
            finally
            {
                _logger.LogDebug("Stopping continuous recognition");
                await recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);

                // Pause briefly to ensure all events are processed
                await Task.Delay(500, CancellationToken.None).ConfigureAwait(false);

                recognizer.Recognized -= eventHandler.Recognized;
                recognizer.Canceled -= eventHandler.Canceled;
                recognizer.SessionStopped -= eventHandler.Stopped;
            }

            var finalText = eventHandler.GetRecognizedText().ToString().Trim();
            _logger.LogDebug("Recognition complete. Final text length={Length}", finalText.Length);

            return new SpeechToTextResponse(finalText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred.");

            return new SpeechToTextResponse(string.Empty);
        }
        finally
        {
            ownedMemoryStream?.Dispose();
        }
    }

    public async IAsyncEnumerable<SpeechToTextResponseUpdate> GetStreamingTextAsync(
        Stream audioSpeechStream,
        SpeechToTextOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audioSpeechStream);

        _logger.LogDebug("GetStreamingTextAsync called. CanSeek={CanSeek}", audioSpeechStream.CanSeek);

        // For streaming scenarios, use the stream directly - don't check Length upfront
        // as the stream may be a producer-consumer stream being written to concurrently.
        // The format detection will naturally wait for data if needed.
        Stream workingStream = audioSpeechStream;

        // Only copy to MemoryStream if the stream is not seekable
        MemoryStream ownedMemoryStream = null;
        if (!audioSpeechStream.CanSeek)
        {
            ownedMemoryStream = new MemoryStream();
            await audioSpeechStream.CopyToAsync(ownedMemoryStream, cancellationToken).ConfigureAwait(false);
            ownedMemoryStream.Position = 0;
            workingStream = ownedMemoryStream;
            _logger.LogDebug("Copied audio to memory stream. Bytes={Length}", ownedMemoryStream.Length);

            if (ownedMemoryStream.Length == 0)
            {
                _logger.LogDebug("GetStreamingTextAsync: stream length is 0; yielding SessionClose");
                ownedMemoryStream.Dispose();
                yield return new SpeechToTextResponseUpdate
                {
                    ResponseId = Guid.NewGuid().ToString(),
                    Kind = SpeechToTextResponseUpdateKind.SessionClose,
                };
                yield break;
            }
        }

        SpeechToTextResponseUpdate textUpdate = null;
        SpeechToTextResponseUpdate closeUpdate = null;

        try
        {
            // Configure language if specified
            if (!string.IsNullOrEmpty(options?.TextLanguage))
            {
                _speechConfig.SpeechRecognitionLanguage = options.TextLanguage;
                _logger.LogDebug("Streaming: Set speech recognition language to '{Language}'", options.TextLanguage);
            }
            else
            {
                _speechConfig.SpeechRecognitionLanguage = DefaultLanguage;
                _logger.LogDebug("Streaming: Using default speech recognition language '{Language}'", DefaultLanguage);
            }

            // Detect audio format - this will wait for data if using a producer-consumer stream
            _logger.LogDebug("Detecting audio format...");
            var detectedFormat = DetectAudioFormat(workingStream);
            workingStream.Position = 0;

            _logger.LogDebug("Detected streaming audio format: {Format}", detectedFormat);

            var responseId = Guid.NewGuid().ToString();

            if (detectedFormat == AudioFormat.Unknown)
            {
                _logger.LogWarning("Unknown audio format detected, cannot transcribe");
                closeUpdate = new SpeechToTextResponseUpdate
                {
                    ResponseId = responseId,
                    Kind = SpeechToTextResponseUpdateKind.SessionClose,
                };
            }
            else
            {
                // Create audio format and recognizer using PullStream pattern
                using var audioFormat = CreateAudioStreamFormat(detectedFormat);
                using var audioInputStream = AudioInputStream.CreatePullStream(new AudioStreamReader(workingStream), audioFormat);
                using var audioConfig = AudioConfig.FromStreamInput(audioInputStream);
                using var recognizer = new SpeechRecognizer(_speechConfig, audioConfig);

                using var manualCancellationTokenSource = new CancellationTokenSource();
                using var combinedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, manualCancellationTokenSource.Token);

                var eventHandler = new RecognizerEventHandler(_logger, manualCancellationTokenSource);

                recognizer.Recognized += eventHandler.Recognized;
                recognizer.Canceled += eventHandler.Canceled;
                recognizer.SessionStopped += eventHandler.Stopped;

                try
                {
                    _logger.LogDebug("Starting continuous recognition");
                    await recognizer.StartContinuousRecognitionAsync().ConfigureAwait(false);

                    // Wait for recognition to complete - cancelled by SessionStopped or Canceled events
                    await Task.Delay(Timeout.Infinite, combinedCancellation.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Recognition was canceled by the caller");
                }
                catch (OperationCanceledException)
                {
                    // Expected when recognition completes normally
                    _logger.LogDebug("Recognition completed normally");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during speech recognition");
                }
                finally
                {
                    _logger.LogDebug("Stopping continuous recognition");
                    await recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);

                    // Pause briefly to ensure all events are processed
                    await Task.Delay(500, CancellationToken.None).ConfigureAwait(false);

                    recognizer.Recognized -= eventHandler.Recognized;
                    recognizer.Canceled -= eventHandler.Canceled;
                    recognizer.SessionStopped -= eventHandler.Stopped;
                }

                // Prepare the response updates
                var finalText = eventHandler.GetRecognizedText().ToString().Trim();
                _logger.LogDebug("Final recognized text length={Length}", finalText.Length);

                if (!string.IsNullOrEmpty(finalText))
                {
                    textUpdate = new SpeechToTextResponseUpdate(finalText)
                    {
                        ResponseId = responseId,
                        Kind = SpeechToTextResponseUpdateKind.TextUpdating,
                    };
                }

                closeUpdate = new SpeechToTextResponseUpdate
                {
                    ResponseId = responseId,
                    Kind = SpeechToTextResponseUpdateKind.SessionClose,
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred during streaming.");
            closeUpdate = new SpeechToTextResponseUpdate
            {
                ResponseId = Guid.NewGuid().ToString(),
                Kind = SpeechToTextResponseUpdateKind.SessionClose,
            };
        }
        finally
        {
            ownedMemoryStream?.Dispose();
        }

        // Yield outside the try-finally to avoid issues with async enumerator
        if (textUpdate != null)
        {
            yield return textUpdate;
        }

        if (closeUpdate != null)
        {
            yield return closeUpdate;
        }
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
        var header = new byte[12];
        var originalPosition = stream.Position;

        stream.Seek(0, SeekOrigin.Begin);
        var read = stream.Read(header, 0, header.Length);
        stream.Seek(originalPosition, SeekOrigin.Begin);

        if (read < 4)
        {
            return AudioFormat.Unknown;
        }

        // WAV (RIFF)
        if (header[0] == 'R' && header[1] == 'I' && header[2] == 'F' && header[3] == 'F')
        {
            return AudioFormat.Wav;
        }

        // OGG/Opus
        if (header[0] == 'O' && header[1] == 'g' && header[2] == 'g' && header[3] == 'S')
        {
            return AudioFormat.OggOpus;
        }

        // FLAC
        if (header[0] == 'f' && header[1] == 'L' && header[2] == 'a' && header[3] == 'C')
        {
            return AudioFormat.Flac;
        }

        // MP3: ID3 tag
        if (header[0] == 'I' && header[1] == 'D' && header[2] == '3')
        {
            return AudioFormat.Mp3;
        }

        // MP3: frame sync 0xFF Ex (MPEG audio frame)
        if (header[0] == 0xFF && (header[1] & 0xE0) == 0xE0)
        {
            return AudioFormat.Mp3;
        }

        // AMR-NB: header starts with "#!AMR\n"
        if (read >= 6)
        {
            var asciiHeader = Encoding.ASCII.GetString(header, 0, 6);
            if (asciiHeader == "#!AMR\n")
            {
                return AudioFormat.AmrNb;
            }
        }

        // AMR-WB: header starts with "#!AMR-WB\n"
        if (read >= 9)
        {
            var asciiHeaderWb = System.Text.Encoding.ASCII.GetString(header, 0, 9);
            if (asciiHeaderWb == "#!AMR-WB\n")
            {
                return AudioFormat.AmrWb;
            }
        }

        return AudioFormat.Unknown;
    }

    /// <summary>
    /// Creates an appropriate AudioStreamFormat based on the detected audio format.
    /// </summary>
    private static AudioStreamFormat CreateAudioStreamFormat(AudioFormat format)
    {
        return format switch
        {
            AudioFormat.Wav => AudioStreamFormat.GetWaveFormatPCM(16000, 16, 1),
            AudioFormat.Mp3 => AudioStreamFormat.GetCompressedFormat(AudioStreamContainerFormat.MP3),
            AudioFormat.OggOpus => AudioStreamFormat.GetCompressedFormat(AudioStreamContainerFormat.OGG_OPUS),
            AudioFormat.Flac => AudioStreamFormat.GetCompressedFormat(AudioStreamContainerFormat.FLAC),
            AudioFormat.Alaw => AudioStreamFormat.GetCompressedFormat(AudioStreamContainerFormat.ALAW),
            AudioFormat.Mulaw => AudioStreamFormat.GetCompressedFormat(AudioStreamContainerFormat.MULAW),
            AudioFormat.AmrNb => AudioStreamFormat.GetCompressedFormat(AudioStreamContainerFormat.AMRNB),
            AudioFormat.AmrWb => AudioStreamFormat.GetCompressedFormat(AudioStreamContainerFormat.AMRWB),
            _ => throw new InvalidOperationException("Unsupported audio format detected.")
        };
    }

    /// <summary>
    /// Adapter class that implements PullAudioInputStreamCallback to read from a Stream.
    /// This is the key component that allows the Azure Speech SDK to pull audio data from a .NET Stream.
    /// </summary>
    private sealed class AudioStreamReader : PullAudioInputStreamCallback
    {
        private readonly BinaryReader _binaryReader;
        private bool _disposed;

        /// <summary>
        /// Creates and initializes an instance of AudioStreamReader.
        /// </summary>
        /// <param name="stream">The underlying stream to read the audio data from.</param>
        public AudioStreamReader(Stream stream)
        {
            // Use leaveOpen: true to prevent the BinaryReader from disposing the underlying stream
            _binaryReader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        }

        /// <summary>
        /// Reads binary data from the stream.
        /// </summary>
        /// <param name="dataBuffer">The buffer to fill</param>
        /// <param name="size">The size of the buffer.</param>
        /// <returns>The number of bytes filled, or 0 in case the stream hits its end and there is no more data available.
        /// If there is no data immediate available, Read() blocks until the next data becomes available.</returns>
        public override int Read(byte[] dataBuffer, uint size)
        {
            var totalRead = 0;

            while (totalRead < (int)size)
            {
                var bytesRead = _binaryReader.Read(dataBuffer, totalRead, (int)size - totalRead);

                if (bytesRead == 0) // EOF
                {
                    break;
                }

                totalRead += bytesRead;
            }

            return totalRead;
        }

        /// <summary>
        /// Get property associated to data buffer, such as a timestamp or userId. if the property is not available, an empty string must be returned. 
        /// </summary>
        /// <param name="id">A property id.</param>
        /// <returns>The value of the property </returns>
        public override string GetProperty(PropertyId id)
        {
            return string.Empty;
        }

        /// <summary>
        /// This method performs cleanup of resources.
        /// </summary>
        /// <param name="disposing">Flag to request disposal.</param>
        protected override void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                _binaryReader.Dispose();
            }

            _disposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class RecognizerEventHandler
    {
        private readonly ILogger _logger;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly StringBuilder _recognizedText = new();

        public RecognizerEventHandler(ILogger logger, CancellationTokenSource cancellationTokenSource)
        {
            _logger = logger;
            _cancellationTokenSource = cancellationTokenSource;
        }

        public StringBuilder GetRecognizedText()
            => _recognizedText;

        public void Recognized(object sender, SpeechRecognitionEventArgs e)
        {
            _logger.LogDebug("Event Recognized. Reason={Reason} Text='{Text}'", e.Result.Reason, e.Result.Text);
            if (e.Result.Reason == ResultReason.RecognizedSpeech && !string.IsNullOrEmpty(e.Result.Text))
            {
                _recognizedText.Append(e.Result.Text);
                _recognizedText.Append(' ');
            }
        }

        public void Canceled(object sender, SpeechRecognitionCanceledEventArgs e)
        {
            _logger.LogWarning("Event Canceled. Reason={Reason} ErrorCode={ErrorCode} ErrorDetails={ErrorDetails}", e.Reason, e.ErrorCode, e.ErrorDetails);
            if (!_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
            }
        }

        public void Stopped(object sender, SessionEventArgs e)
        {
            _logger.LogDebug("Event SessionStopped");
            if (!_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
            }
        }
    }
}
#pragma warning restore MEAI001
