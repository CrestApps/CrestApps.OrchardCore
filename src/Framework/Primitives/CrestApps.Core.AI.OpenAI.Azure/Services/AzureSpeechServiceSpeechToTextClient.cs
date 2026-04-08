using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Azure.Core;
using Azure.Identity;
using CrestApps.Core.AI.Services;
using CrestApps.Core.Azure.Models;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CrestApps.Core.AI.OpenAI.Azure.Services;

/// <summary>
/// An <see cref="ISpeechToTextClient"/> implementation that uses the Azure Speech SDK
/// for speech-to-text recognition. Supports continuous recognition for real-time streaming.
/// </summary>
/// <remarks>
/// When the endpoint matches the standard Azure Cognitive Services pattern
/// (<c>{region}.api.cognitive.microsoft.com</c>), the region is extracted and
/// <see cref="SpeechConfig.FromSubscription"/> / <see cref="SpeechConfig.FromAuthorizationToken"/>
/// are used so that the SDK constructs the correct WebSocket URLs internally.
/// For custom-domain endpoints, <see cref="SpeechConfig.FromEndpoint"/> is used as a fallback.
/// </remarks>
#pragma warning disable MEAI001
public sealed class AzureSpeechServiceSpeechToTextClient : ISpeechToTextClient
#pragma warning restore MEAI001
{
    private const string CognitiveServicesScope = "https://cognitiveservices.azure.com/.default";

    private static readonly string[] _regionSuffixes =
    [
        ".api.cognitive.microsoft.com",
        ".tts.speech.microsoft.com",
        ".stt.speech.microsoft.com",
    ];

    private readonly Uri _endpoint;
    private readonly AzureAuthenticationType _authType;
    private readonly string _apiKey;
    private readonly string _identityId;
    private readonly string _region;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    private string _cachedToken;
    private DateTimeOffset _tokenExpires;

    public AzureSpeechServiceSpeechToTextClient(
        Uri endpoint,
        AzureAuthenticationType authType,
        string apiKey,
        string identityId,
        TimeProvider timeProvider,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        _endpoint = endpoint;
        _authType = authType;
        _apiKey = apiKey;
        _identityId = identityId;
        _region = TryExtractRegion(endpoint);
        _timeProvider = timeProvider;
        _logger = logger;
    }

#pragma warning disable MEAI001
    public async Task<SpeechToTextResponse> GetTextAsync(
        Stream audioSpeechStream,
        SpeechToTextOptions options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(audioSpeechStream);

        var traceId = Guid.NewGuid().ToString("N")[..8];
        var sw = Stopwatch.StartNew();
        var language = SpeechLanguageHelper.NormalizeOrDefault(options?.SpeechLanguage);

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("[STT:{TraceId}] +{Elapsed}ms GetTextAsync START. Language={Language}, AuthType={AuthType}",
            traceId, sw.ElapsedMilliseconds, language, _authType);
        }

        var speechConfig = await CreateSpeechConfigAsync(language, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("[STT:{TraceId}] +{Elapsed}ms SpeechConfig created.", traceId, sw.ElapsedMilliseconds);
        }

        var containerFormat = ResolveContainerFormat(options);
        var audioFormat = AudioStreamFormat.GetCompressedFormat(containerFormat);
        using var pushStream = AudioInputStream.CreatePushStream(audioFormat);
        using var audioConfig = AudioConfig.FromStreamInput(pushStream);
        using var recognizer = CreateRecognizer(speechConfig, audioConfig);

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("[STT:{TraceId}] +{Elapsed}ms Recognizer created. Format={Format}. Pushing audio...",
            traceId, sw.ElapsedMilliseconds, containerFormat);
        }

        var buffer = new byte[1024];
        int bytesRead;
        var totalBytes = 0L;

        while ((bytesRead = await audioSpeechStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
        {
            pushStream.Write(buffer, bytesRead);
            totalBytes += bytesRead;
        }

        pushStream.Close();

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("[STT:{TraceId}] +{Elapsed}ms Audio push complete. TotalBytes={TotalBytes}. Starting RecognizeOnceAsync...",
            traceId, sw.ElapsedMilliseconds, totalBytes);
        }

        var result = await recognizer.RecognizeOnceAsync();

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("[STT:{TraceId}] +{Elapsed}ms RecognizeOnceAsync returned. Reason={Reason}",
            traceId, sw.ElapsedMilliseconds, result.Reason);
        }

        if (result.Reason == ResultReason.RecognizedSpeech)
        {
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("[STT:{TraceId}] +{Elapsed}ms SUCCESS. Text='{Text}'",
                traceId, sw.ElapsedMilliseconds, result.Text);
            }

            return new SpeechToTextResponse(result.Text);
        }

        if (result.Reason == ResultReason.NoMatch)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("[STT:{TraceId}] +{Elapsed}ms NoMatch from {TotalBytes} bytes.",
                traceId, sw.ElapsedMilliseconds, totalBytes);





            }
        }
        else if (result.Reason == ResultReason.Canceled)
        {
            var cancellation = CancellationDetails.FromResult(result);
            _logger.LogWarning(
                "[STT:{TraceId}] +{Elapsed}ms CANCELED. Reason={Reason}, ErrorCode={ErrorCode}, Details={ErrorDetails}",
                traceId, sw.ElapsedMilliseconds, cancellation.Reason, cancellation.ErrorCode, cancellation.ErrorDetails);
        }

        return null;
    }

    public async IAsyncEnumerable<SpeechToTextResponseUpdate> GetStreamingTextAsync(
        Stream audioSpeechStream,
        SpeechToTextOptions options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(audioSpeechStream);

        var traceId = Guid.NewGuid().ToString("N")[..8];
        var sw = Stopwatch.StartNew();
        var language = SpeechLanguageHelper.NormalizeOrDefault(options?.SpeechLanguage);

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("[STT:{TraceId}] +{Elapsed}ms GetStreamingTextAsync START. Language={Language}, AuthType={AuthType}",
            traceId, sw.ElapsedMilliseconds, language, _authType);
        }

        var speechConfig = await CreateSpeechConfigAsync(language, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("[STT:{TraceId}] +{Elapsed}ms SpeechConfig created.", traceId, sw.ElapsedMilliseconds);
        }

        var containerFormat = ResolveContainerFormat(options);
        var audioFormat = AudioStreamFormat.GetCompressedFormat(containerFormat);
        using var pushStream = AudioInputStream.CreatePushStream(audioFormat);
        using var audioConfig = AudioConfig.FromStreamInput(pushStream);
        using var recognizer = CreateRecognizer(speechConfig, audioConfig);

        // Create a linked CancellationTokenSource so SDK errors can immediately stop the push task.
        using var errorCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var channel = Channel.CreateUnbounded<SpeechToTextResponseUpdate>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("[STT:{TraceId}] +{Elapsed}ms Recognizer created. Format={Format}. Wiring events...",
            traceId, sw.ElapsedMilliseconds, containerFormat);
        }

        recognizer.Recognizing += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Result.Text))
            {
                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTrace("[STT:{TraceId}] +{Elapsed}ms PARTIAL: '{Text}'",
                    traceId, sw.ElapsedMilliseconds, e.Result.Text);
                }

                channel.Writer.TryWrite(new SpeechToTextResponseUpdate(e.Result.Text)
                {
                    AdditionalProperties = new AdditionalPropertiesDictionary
                    {
                        ["isPartial"] = true,
                    },
                });
            }
        };

        recognizer.Recognized += (_, e) =>
        {
            if (e.Result.Reason == ResultReason.RecognizedSpeech && !string.IsNullOrEmpty(e.Result.Text))
            {
                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTrace("[STT:{TraceId}] +{Elapsed}ms FINAL: '{Text}'",
                    traceId, sw.ElapsedMilliseconds, e.Result.Text);
                }

                channel.Writer.TryWrite(new SpeechToTextResponseUpdate(e.Result.Text));
            }
            else if (e.Result.Reason == ResultReason.NoMatch)
            {
                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTrace("[STT:{TraceId}] +{Elapsed}ms NoMatch segment.", traceId, sw.ElapsedMilliseconds);
                }
            }
        };

        recognizer.SessionStarted += (_, e) =>
        {
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("[STT:{TraceId}] +{Elapsed}ms SessionStarted. SdkSessionId={SessionId}",
                traceId, sw.ElapsedMilliseconds, e.SessionId);
            }
        };

        recognizer.SessionStopped += (_, e) =>
        {
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("[STT:{TraceId}] +{Elapsed}ms SessionStopped. SdkSessionId={SessionId}",
                traceId, sw.ElapsedMilliseconds, e.SessionId);
            }

            channel.Writer.TryComplete();
        };

        recognizer.Canceled += (_, e) =>
        {
            if (e.Reason == CancellationReason.Error)
            {
                _logger.LogWarning(
                    "[STT:{TraceId}] +{Elapsed}ms CANCELED with error. ErrorCode={ErrorCode}, Details={ErrorDetails}",
                    traceId, sw.ElapsedMilliseconds, e.ErrorCode, e.ErrorDetails);

                errorCts.Cancel();
                pushStream.Close();
                channel.Writer.TryComplete(new InvalidOperationException($"Error Code: {e.ErrorCode}. Error Details: {e.ErrorDetails}"));
            }
            else
            {
                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTrace("[STT:{TraceId}] +{Elapsed}ms CANCELED (non-error). Reason={Reason}",
                    traceId, sw.ElapsedMilliseconds, e.Reason);
                }

                channel.Writer.TryComplete();
            }
        };

        // Start pushing audio data immediately so the recognizer receives data as soon as it starts.
        var pushTask = PushAudioToStreamAsync(traceId, sw, audioSpeechStream, pushStream, errorCts.Token);

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("[STT:{TraceId}] +{Elapsed}ms Starting continuous recognition...", traceId, sw.ElapsedMilliseconds);
        }

        await recognizer.StartContinuousRecognitionAsync();

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("[STT:{TraceId}] +{Elapsed}ms Continuous recognition started. Reading channel...", traceId, sw.ElapsedMilliseconds);
        }

        // When the caller cancels, complete the channel so ReadAllAsync
        // finishes gracefully instead of throwing OperationCanceledException.
        using var ctr = cancellationToken.Register(() => channel.Writer.TryComplete());

        await foreach (var update in channel.Reader.ReadAllAsync(CancellationToken.None))
        {
            yield return update;
        }

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("[STT:{TraceId}] +{Elapsed}ms Channel drained. Awaiting push task...", traceId, sw.ElapsedMilliseconds);
        }

        try
        {
            await pushTask;
        }
        catch (OperationCanceledException)
        {
            // Expected when the connection is closed or recognition is stopped.
        }

        try
        {
            await recognizer.StopContinuousRecognitionAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[STT:{TraceId}] Error stopping continuous recognition.", traceId);
        }

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("[STT:{TraceId}] +{Elapsed}ms GetStreamingTextAsync COMPLETE.", traceId, sw.ElapsedMilliseconds);
        }
    }
#pragma warning restore MEAI001

    private async Task PushAudioToStreamAsync(
        string traceId,
        Stopwatch sw,
        Stream audioSpeechStream,
        PushAudioInputStream pushStream,
        CancellationToken cancellationToken)
    {
        var totalBytes = 0L;
        var chunkCount = 0;

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("[STT:{TraceId}] +{Elapsed}ms PushAudioToStream START.", traceId, sw.ElapsedMilliseconds);
        }

        try
        {
            var buffer = new byte[1024];
            int bytesRead;

            while ((bytesRead = await audioSpeechStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
            {
                pushStream.Write(buffer, bytesRead);
                totalBytes += bytesRead;
                chunkCount++;

                // Log every chunk to trace latency between chunks.
                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTrace("[STT:{TraceId}] +{Elapsed}ms PushChunk #{ChunkCount}: {BytesRead} bytes (total={TotalBytes})",
                    traceId, sw.ElapsedMilliseconds, chunkCount, bytesRead, totalBytes);
                }
            }

            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("[STT:{TraceId}] +{Elapsed}ms PushAudioToStream DONE. Chunks={ChunkCount}, TotalBytes={TotalBytes}",
                traceId, sw.ElapsedMilliseconds, chunkCount, totalBytes);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("[STT:{TraceId}] +{Elapsed}ms PushAudioToStream CANCELED after {TotalBytes} bytes.",
                traceId, sw.ElapsedMilliseconds, totalBytes);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[STT:{TraceId}] +{Elapsed}ms PushAudioToStream ERROR after {TotalBytes} bytes.",
            traceId, sw.ElapsedMilliseconds, totalBytes);
        }
        finally
        {
            pushStream.Close();
        }
    }

    public object GetService(Type serviceType, object serviceKey = null)
    {
#pragma warning disable MEAI001
        if (serviceType == typeof(SpeechToTextClientMetadata))
        {
            return new SpeechToTextClientMetadata("AzureSpeech", _endpoint);
        }
#pragma warning restore MEAI001

        return null;
    }

    public void Dispose()
    {
        _tokenLock.Dispose();
    }

    private async Task<SpeechConfig> CreateSpeechConfigAsync(string language, CancellationToken cancellationToken)
    {
        SpeechConfig config;

        if (_authType == AzureAuthenticationType.ApiKey)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Using endpoint-based SDK configuration for API key authentication. Endpoint: {Endpoint}, Region: {Region}",
                    _endpoint,
                    _region ?? "(unknown)");
            }

            config = await CreateEndpointBasedConfigAsync(cancellationToken);
        }
        else if (_region != null)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Using region '{Region}' for SDK configuration.", _region);
            }

            config = await CreateRegionBasedConfigAsync(_region, cancellationToken);
        }
        else
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Could not extract region from endpoint '{Endpoint}'. Falling back to endpoint-based SDK configuration.",
                    _endpoint);
            }

            config = await CreateEndpointBasedConfigAsync(cancellationToken);
        }

        config.SpeechRecognitionLanguage = language;

        // Use reasonable timeouts: long enough for audio to arrive over the network,
        // short enough to avoid excessive delay after the user stops speaking.
        config.SetProperty(PropertyId.SpeechServiceConnection_InitialSilenceTimeoutMs, "3000");
        config.SetProperty(PropertyId.SpeechServiceConnection_EndSilenceTimeoutMs, "1000");

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace(
                "SpeechConfig created. Language: {Language}, AuthType: {AuthType}, Endpoint: {Endpoint}, Region: {Region}",
                language,
                _authType,
                _endpoint,
                _region ?? "(from endpoint)");
        }

        return config;
    }

    private async Task<SpeechConfig> CreateRegionBasedConfigAsync(string region, CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            var maskedKey = !string.IsNullOrEmpty(_apiKey)
            ? "[Redacted]"
            : "(not set)";

            _logger.LogDebug(
                "Creating region-based SpeechConfig. Region: {Region}, AuthType: {AuthType}, ApiKey: {MaskedKey}, IdentityId: {IdentityId}",
                region, _authType, maskedKey, string.IsNullOrEmpty(_identityId) ? "(system)" : _identityId);
        }

        return _authType switch
        {
            AzureAuthenticationType.ApiKey
                => SpeechConfig.FromSubscription(_apiKey, region),

            AzureAuthenticationType.ManagedIdentity or AzureAuthenticationType.Default
                => SpeechConfig.FromAuthorizationToken(
                    await GetAuthorizationTokenAsync(cancellationToken), region),

            _ => throw new NotSupportedException(
                $"Authentication type '{_authType}' is not supported for Azure Speech."),
        };
    }

    private async Task<SpeechConfig> CreateEndpointBasedConfigAsync(CancellationToken cancellationToken)
    {
        return _authType switch
        {
            AzureAuthenticationType.ApiKey
                => SpeechConfig.FromEndpoint(_endpoint, _apiKey),

            AzureAuthenticationType.ManagedIdentity or AzureAuthenticationType.Default
                => CreateEndpointConfigWithToken(
                    await GetAuthorizationTokenAsync(cancellationToken)),

            _ => throw new NotSupportedException(
                $"Authentication type '{_authType}' is not supported for Azure Speech."),
        };
    }

    private SpeechConfig CreateEndpointConfigWithToken(string token)
    {
        var config = SpeechConfig.FromEndpoint(_endpoint);
        config.AuthorizationToken = token;

        return config;
    }

    private async Task<string> GetAuthorizationTokenAsync(CancellationToken cancellationToken)
    {
        await _tokenLock.WaitAsync(cancellationToken);

        try
        {
            if (_cachedToken != null && _tokenExpires > _timeProvider.GetUtcNow().AddMinutes(-1))
            {
                return _cachedToken;
            }

            TokenCredential credential = _authType switch
            {
                AzureAuthenticationType.ManagedIdentity => string.IsNullOrEmpty(_identityId)
                ? new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned)
                : new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId(_identityId)),
                _ => new DefaultAzureCredential(),
            };

            var tokenResult = await credential.GetTokenAsync(
                new TokenRequestContext([CognitiveServicesScope]),
            cancellationToken);

            _cachedToken = tokenResult.Token;
            _tokenExpires = tokenResult.ExpiresOn;

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Successfully obtained authorization token for Azure Speech. AuthType: {AuthType}", _authType);
            }

            return _cachedToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }
    /// <summary>
    /// Attempts to extract the Azure region from a well-known Speech / Cognitive Services endpoint URL.
    /// Supports patterns such as:
    /// <list type="bullet">
    ///   <item><c>https://{region}.api.cognitive.microsoft.com/</c></item>
    ///   <item><c>https://{region}.tts.speech.microsoft.com/</c></item>
    ///   <item><c>https://{region}.stt.speech.microsoft.com/</c></item>
    /// </list>
    /// </summary>
    /// <returns>The region string (e.g. <c>eastus</c>), or <c>null</c> if the pattern does not match.</returns>
    private static string TryExtractRegion(Uri endpoint)
    {
        var host = endpoint.Host;

        foreach (var suffix in _regionSuffixes)
        {
            if (host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                var region = host[..^suffix.Length];

                if (!string.IsNullOrEmpty(region))
                {
                    return region;
                }
            }
        }

        return null;
    }
    /// <summary>
    /// Reads an optional <c>audioFormat</c> value from <see cref="SpeechToTextOptions.AdditionalProperties"/>
    /// and maps it to an <see cref="AudioStreamContainerFormat"/>. Falls back to
    /// <see cref="AudioStreamContainerFormat.ANY"/> when the value is missing or unrecognized,
    /// which lets the SDK auto-detect the container format.
    /// </summary>
#pragma warning disable MEAI001
    private AudioStreamContainerFormat ResolveContainerFormat(SpeechToTextOptions options)
#pragma warning restore MEAI001
    {
        if (options?.AdditionalProperties is not null &&
            options.AdditionalProperties.TryGetValue("audioFormat", out var formatValue) &&
                formatValue is string formatString &&
                    !string.IsNullOrWhiteSpace(formatString))
        {
            var resolved = MapToContainerFormat(formatString);

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Resolved audio format '{FormatString}' to {ContainerFormat}.", formatString, resolved);
            }

            return resolved;
        }

        return AudioStreamContainerFormat.ANY;
    }
    /// <summary>
    /// Maps a MIME-type or short name to an <see cref="AudioStreamContainerFormat"/> value.
    /// Returns <see cref="AudioStreamContainerFormat.ANY"/> for unrecognized values.
    /// Browsers typically produce OGG/Opus or WebM/Opus from MediaRecorder; both map to
    /// <see cref="AudioStreamContainerFormat.OGG_OPUS"/> because the Opus codec payload
    /// is identical and the Azure Speech SDK natively supports OGG/Opus decoding.
    /// </summary>
    private static AudioStreamContainerFormat MapToContainerFormat(string format)
    {
        var normalized = format.Trim().ToLowerInvariant();

        // If the full MIME type explicitly mentions the opus codec, use OGG_OPUS
        // regardless of the container (ogg, webm, etc.).
        if (normalized.Contains("opus"))
        {
            return AudioStreamContainerFormat.OGG_OPUS;
        }

        // Strip codec parameters for base MIME type matching.
        var semicolonIndex = normalized.IndexOf(';');

        if (semicolonIndex >= 0)
        {
            normalized = normalized[..semicolonIndex].TrimEnd();
        }

        return normalized switch
        {
            "audio/ogg" or "ogg_opus" or "ogg" => AudioStreamContainerFormat.OGG_OPUS,
            "audio/mp3" or "audio/mpeg" or "mp3" => AudioStreamContainerFormat.MP3,
            "audio/flac" or "flac" => AudioStreamContainerFormat.FLAC,
            "audio/alaw" or "alaw" => AudioStreamContainerFormat.ALAW,
            "audio/mulaw" or "mulaw" => AudioStreamContainerFormat.MULAW,
            "audio/amr" or "amrnb" => AudioStreamContainerFormat.AMRNB,
            "audio/amr-wb" or "amrwb" => AudioStreamContainerFormat.AMRWB,
            _ => AudioStreamContainerFormat.ANY,
        };
    }
    /// <summary>
    /// Creates a <see cref="SpeechRecognizer"/> and wraps the constructor to provide a clear error
    /// message when GStreamer is missing (required for compressed audio formats on all platforms).
    /// </summary>
    private static SpeechRecognizer CreateRecognizer(SpeechConfig speechConfig, AudioConfig audioConfig)
    {
        try
        {
            return new SpeechRecognizer(speechConfig, audioConfig);
        }
        catch (ApplicationException ex) when (IsGStreamerError(ex))
        {
            throw new InvalidOperationException(
                "Azure Speech SDK requires GStreamer to decode compressed audio. " +
                "Install GStreamer from https://gstreamer.freedesktop.org/download/ and ensure " +
                "the binaries are in the system PATH. " +
                "See: https://learn.microsoft.com/en-us/azure/ai-services/speech-service/how-to-use-codec-compressed-audio-input-streams",
                ex);
        }
    }
    /// <summary>
    /// Checks whether an exception is the GStreamer-not-found error (SPXERR_GSTREAMER_NOT_FOUND_ERROR = 0x29).
    /// </summary>
    private static bool IsGStreamerError(ApplicationException ex)
        => ex.Message.Contains("0x29", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("GSTREAMER", StringComparison.OrdinalIgnoreCase);
}
