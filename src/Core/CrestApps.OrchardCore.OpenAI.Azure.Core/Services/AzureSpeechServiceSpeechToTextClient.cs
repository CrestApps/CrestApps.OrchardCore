using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Azure.Core;
using Azure.Identity;
using CrestApps.Azure.Core.Models;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Services;

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
    private readonly ILogger _logger;

    public AzureSpeechServiceSpeechToTextClient(
        Uri endpoint,
        AzureAuthenticationType authType,
        string apiKey,
        string identityId,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        _endpoint = endpoint;
        _authType = authType;
        _apiKey = apiKey;
        _identityId = identityId;
        _logger = logger;
    }

#pragma warning disable MEAI001
    public async Task<SpeechToTextResponse> GetTextAsync(
        Stream audioSpeechStream,
        SpeechToTextOptions options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(audioSpeechStream);

        var language = options?.SpeechLanguage ?? "en-US";

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Starting single-shot speech recognition. Language: {Language}, Endpoint: {Endpoint}, AuthType: {AuthType}",
                language, _endpoint, _authType);
        }

        var speechConfig = await CreateSpeechConfigAsync(language, cancellationToken);

        var containerFormat = ResolveContainerFormat(options);
        var audioFormat = AudioStreamFormat.GetCompressedFormat(containerFormat);
        using var pushStream = AudioInputStream.CreatePushStream(audioFormat);
        using var audioConfig = AudioConfig.FromStreamInput(pushStream);
        using var recognizer = new SpeechRecognizer(speechConfig, audioConfig);

        var buffer = new byte[4096];
        int bytesRead;
        var totalBytes = 0L;

        while ((bytesRead = await audioSpeechStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
        {
            pushStream.Write(buffer, bytesRead);
            totalBytes += bytesRead;
        }

        pushStream.Close();

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Pushed {TotalBytes} bytes of audio data for single-shot recognition.", totalBytes);
        }

        var result = await recognizer.RecognizeOnceAsync();

        if (result.Reason == ResultReason.RecognizedSpeech)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Single-shot recognition succeeded. Text length: {TextLength}", result.Text?.Length ?? 0);
            }

            return new SpeechToTextResponse(result.Text);
        }

        if (result.Reason == ResultReason.NoMatch)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Azure Speech SDK: No speech could be recognized from {TotalBytes} bytes of audio.", totalBytes);
            }
        }
        else if (result.Reason == ResultReason.Canceled)
        {
            var cancellation = CancellationDetails.FromResult(result);
            _logger.LogWarning(
                "Azure Speech SDK recognition canceled: Reason={Reason}, ErrorCode={ErrorCode}, ErrorDetails={ErrorDetails}",
                cancellation.Reason, cancellation.ErrorCode, cancellation.ErrorDetails);
        }

        return null;
    }

    public async IAsyncEnumerable<SpeechToTextResponseUpdate> GetStreamingTextAsync(
        Stream audioSpeechStream,
        SpeechToTextOptions options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(audioSpeechStream);

        var language = options?.SpeechLanguage ?? "en-US";

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Starting continuous speech recognition. Language: {Language}, Endpoint: {Endpoint}, AuthType: {AuthType}",
                language, _endpoint, _authType);
        }

        var speechConfig = await CreateSpeechConfigAsync(language, cancellationToken);

        var containerFormat = ResolveContainerFormat(options);
        var audioFormat = AudioStreamFormat.GetCompressedFormat(containerFormat);
        using var pushStream = AudioInputStream.CreatePushStream(audioFormat);
        using var audioConfig = AudioConfig.FromStreamInput(pushStream);
        using var recognizer = new SpeechRecognizer(speechConfig, audioConfig);

        // Create a linked CancellationTokenSource so SDK errors can immediately stop the push task.
        using var errorCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var channel = Channel.CreateUnbounded<SpeechToTextResponseUpdate>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

        recognizer.Recognizing += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Result.Text))
            {
                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTrace("Continuous recognition partial result: '{Text}'", e.Result.Text);
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
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Continuous recognition final segment: '{Text}'", e.Result.Text);
                }

                channel.Writer.TryWrite(new SpeechToTextResponseUpdate(e.Result.Text));
            }
            else if (e.Result.Reason == ResultReason.NoMatch)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Continuous recognition segment: No match.");
                }
            }
        };

        recognizer.SessionStarted += (_, e) =>
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Azure Speech SDK session started. SessionId: {SessionId}", e.SessionId);
            }
        };

        recognizer.SessionStopped += (_, e) =>
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Azure Speech SDK session stopped. SessionId: {SessionId}", e.SessionId);
            }

            channel.Writer.TryComplete();
        };

        recognizer.Canceled += (_, e) =>
        {
            if (e.Reason == CancellationReason.Error)
            {
                _logger.LogWarning(
                    "Azure Speech SDK streaming canceled with error. ErrorCode={ErrorCode}, ErrorDetails={ErrorDetails}",
                    e.ErrorCode, e.ErrorDetails);

                // Signal the push task to stop immediately so we don't keep pushing audio after a fatal error.
                errorCts.Cancel();

                // Close the push stream to unblock the recognizer if it's waiting for data.
                pushStream.Close();

                channel.Writer.TryComplete(new InvalidOperationException(e.ErrorDetails));
            }
            else
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Azure Speech SDK streaming ended with reason: {Reason}", e.Reason);
                }

                channel.Writer.TryComplete();
            }
        };

        await recognizer.StartContinuousRecognitionAsync();

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Continuous recognition started. Pushing audio data...");
        }

        // Push audio data from the input stream into the SDK's push stream in the background.
        var pushTask = Task.Run(async () =>
        {
            var totalBytes = 0L;

            try
            {
                var buffer = new byte[4096];
                int bytesRead;

                while ((bytesRead = await audioSpeechStream.ReadAsync(buffer.AsMemory(0, buffer.Length), errorCts.Token)) > 0)
                {
                    pushStream.Write(buffer, bytesRead);
                    totalBytes += bytesRead;
                }

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Finished pushing audio data. Total bytes: {TotalBytes}", totalBytes);
                }
            }
            catch (OperationCanceledException) when (errorCts.IsCancellationRequested)
            {
                _logger.LogWarning("Audio push was cancelled due to an SDK error after {TotalBytes} bytes.", totalBytes);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error while pushing audio data after {TotalBytes} bytes.", totalBytes);
            }
            finally
            {
                pushStream.Close();
            }
        }, errorCts.Token);

        await foreach (var update in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return update;
        }

        await pushTask;

        try
        {
            await recognizer.StopContinuousRecognitionAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error stopping continuous recognition.");
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Continuous recognition completed.");
        }
    }
#pragma warning restore MEAI001

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
        // No owned resources to dispose; SpeechConfig/recognizers are disposed per-call.
    }

    private async Task<SpeechConfig> CreateSpeechConfigAsync(string language, CancellationToken cancellationToken)
    {
        var region = TryExtractRegion(_endpoint);
        SpeechConfig config;

        if (region != null)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Extracted region '{Region}' from endpoint. Using region-based SDK configuration.", region);
            }

            config = await CreateRegionBasedConfigAsync(region, cancellationToken);
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

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("SpeechConfig created. Language: {Language}, Region: {Region}", language, region ?? "(from endpoint)");
        }

        return config;
    }

    private async Task<SpeechConfig> CreateRegionBasedConfigAsync(string region, CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            var maskedKey = !string.IsNullOrEmpty(_apiKey) && _apiKey.Length > 4
                ? _apiKey[..4] + "****"
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

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Successfully obtained authorization token for Azure Speech. AuthType: {AuthType}", _authType);
        }

        return tokenResult.Token;
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
    /// <see cref="AudioStreamContainerFormat.ANY"/> when the value is missing or unrecognized.
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
    /// </summary>
    private static AudioStreamContainerFormat MapToContainerFormat(string format)
    {
        // Normalize: lower-case and strip codec parameters (e.g., "audio/ogg;codecs=opus" → "audio/ogg").
        var normalized = format.Trim().ToLowerInvariant();
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
}
