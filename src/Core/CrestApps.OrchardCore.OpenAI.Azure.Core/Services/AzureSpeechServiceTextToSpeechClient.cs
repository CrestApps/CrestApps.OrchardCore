using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Azure.Core;
using Azure.Identity;
using CrestApps.Azure.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.CognitiveServices.Speech;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

#pragma warning disable MEAI001 // Text-to-speech APIs from Microsoft.Extensions.AI are preview and require explicit opt-in at each usage site.
namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Services;

/// <summary>
/// An <see cref="ITextToSpeechClient"/> implementation that uses the Azure Speech SDK
/// for text-to-speech synthesis. Supports streaming audio output via <see cref="SpeechSynthesizer"/>.
/// </summary>
public sealed class AzureSpeechServiceTextToSpeechClient : ITextToSpeechClient
{
    private const string CognitiveServicesScope = "https://cognitiveservices.azure.com/.default";
    private const string DefaultContentType = "audio/mp3";

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
    private readonly ILogger _logger;

    private string _cachedToken;
    private DateTimeOffset _tokenExpires;

    public AzureSpeechServiceTextToSpeechClient(
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
        _region = TryExtractRegion(endpoint);
        _logger = logger;
    }

    public async Task<TextToSpeechResponse> GetAudioAsync(
        string text,
        TextToSpeechOptions options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Starting single-shot speech synthesis. VoiceId: {VoiceId}, Endpoint: {Endpoint}, AuthType: {AuthType}",
                options?.VoiceId ?? "(default)", _endpoint, _authType);
        }

        var speechConfig = await CreateSpeechConfigAsync(options, cancellationToken);

        // Use null audio config to get in-memory audio result.
        using var synthesizer = new SpeechSynthesizer(speechConfig, null);
        var result = await synthesizer.SpeakTextAsync(text);

        if (result.Reason == ResultReason.SynthesizingAudioCompleted)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Single-shot synthesis succeeded. Audio bytes: {AudioBytes}", result.AudioData.Length);
            }

            return new TextToSpeechResponse(new List<AIContent>
            {
                new DataContent(result.AudioData, DefaultContentType),
            });
        }

        if (result.Reason == ResultReason.Canceled)
        {
            var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
            _logger.LogWarning(
                "Azure Speech SDK synthesis canceled: Reason={Reason}, ErrorCode={ErrorCode}, ErrorDetails={ErrorDetails}",
                cancellation.Reason, cancellation.ErrorCode, cancellation.ErrorDetails);

            throw new InvalidOperationException(
                $"Speech synthesis canceled: {cancellation.ErrorCode} - {cancellation.ErrorDetails}");
        }

        return null;
    }

    public async IAsyncEnumerable<TextToSpeechResponseUpdate> GetStreamingAudioAsync(
        string text,
        TextToSpeechOptions options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Starting streaming speech synthesis. VoiceId: {VoiceId}, Endpoint: {Endpoint}, AuthType: {AuthType}",
                options?.VoiceId ?? "(default)", _endpoint, _authType);
        }

        var speechConfig = await CreateSpeechConfigAsync(options, cancellationToken);

        // Use null audio config to get in-memory audio.
        using var synthesizer = new SpeechSynthesizer(speechConfig, null);

        var channel = Channel.CreateUnbounded<TextToSpeechResponseUpdate>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

        synthesizer.Synthesizing += (_, e) =>
        {
            if (e.Result.AudioData?.Length > 0)
            {
                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTrace("Streaming synthesis chunk: {AudioBytes} bytes", e.Result.AudioData.Length);
                }

                channel.Writer.TryWrite(new TextToSpeechResponseUpdate(new List<AIContent>
                {
                    new DataContent(e.Result.AudioData, DefaultContentType),
                })
                {
                    Kind = TextToSpeechResponseUpdateKind.AudioUpdating,
                });
            }
        };

        synthesizer.SynthesisCompleted += (_, e) =>
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Streaming synthesis completed. Total audio length: {AudioDuration}", e.Result.AudioDuration);
            }

            channel.Writer.TryComplete();
        };

        synthesizer.SynthesisCanceled += (_, e) =>
        {
            var cancellation = SpeechSynthesisCancellationDetails.FromResult(e.Result);

            if (cancellation.Reason == CancellationReason.Error)
            {
                _logger.LogWarning(
                    "Azure Speech SDK streaming synthesis canceled with error. ErrorCode={ErrorCode}, ErrorDetails={ErrorDetails}",
                    cancellation.ErrorCode, cancellation.ErrorDetails);

                channel.Writer.TryComplete(new InvalidOperationException(cancellation.ErrorDetails));
            }
            else
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Azure Speech SDK streaming synthesis ended with reason: {Reason}", cancellation.Reason);
                }

                channel.Writer.TryComplete();
            }
        };

        // Start synthesis — this returns once synthesis begins, not when it finishes.
        var speakTask = synthesizer.StartSpeakingTextAsync(text);

        await foreach (var update in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return update;
        }

        // Await the task to propagate any startup exceptions.
        await speakTask;

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Streaming synthesis iteration completed.");
        }
    }

    /// <summary>
    /// Gets the available voices for text-to-speech synthesis.
    /// </summary>
    /// <param name="locale">An optional locale to filter voices (e.g., "en-US"). If <c>null</c>, all voices are returned.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An array of available <see cref="SpeechVoice"/> instances.</returns>
    public async Task<SpeechVoice[]> GetVoicesAsync(
        string locale = null,
        CancellationToken cancellationToken = default)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Fetching available voices. Locale: {Locale}, Endpoint: {Endpoint}", locale ?? "(all)", _endpoint);
        }

        var speechConfig = await CreateSpeechConfigAsync(null, cancellationToken);

        using var synthesizer = new SpeechSynthesizer(speechConfig, null);
        var result = await synthesizer.GetVoicesAsync(locale ?? string.Empty);

        if (result.Reason == ResultReason.VoicesListRetrieved)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Retrieved {VoiceCount} voices.", result.Voices.Count);
            }

            return result.Voices
                .Select(v => new SpeechVoice
                {
                    Id = v.ShortName,
                    Name = v.LocalName,
                    Language = v.Locale,
                    Gender = MapGender(v.Gender),
                })
                .ToArray();
        }

        _logger.LogWarning("Failed to retrieve voices. Reason: {Reason}", result.Reason);

        return [];
    }

    /// <inheritdoc/>
    public object GetService(Type serviceType, object serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        return serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;
    }

    public void Dispose()
    {
        // No owned resources to dispose; SpeechConfig/synthesizers are disposed per-call.
    }

    private async Task<SpeechConfig> CreateSpeechConfigAsync(TextToSpeechOptions options, CancellationToken cancellationToken)
    {
        SpeechConfig config;

        if (_region != null)
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

        if (!string.IsNullOrEmpty(options?.VoiceId))
        {
            config.SpeechSynthesisVoiceName = options.VoiceId;

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Using voice: {VoiceId}", options.VoiceId);
            }
        }

        if (!string.IsNullOrEmpty(options?.Language))
        {
            config.SpeechSynthesisLanguage = options.Language;
        }

        // Default to MP3 output for browser compatibility.
        config.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Audio16Khz32KBitRateMonoMp3);

        return config;
    }

    private async Task<SpeechConfig> CreateRegionBasedConfigAsync(string region, CancellationToken cancellationToken)
    {
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
        if (_cachedToken != null && _tokenExpires > DateTimeOffset.UtcNow.AddMinutes(-1))
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
            _logger.LogDebug("Successfully obtained authorization token for Azure Speech TTS. AuthType: {AuthType}", _authType);
        }

        return _cachedToken;
    }

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

    private static SpeechVoiceGender MapGender(SynthesisVoiceGender gender)
    {
        return gender switch
        {
            SynthesisVoiceGender.Male => SpeechVoiceGender.Male,
            SynthesisVoiceGender.Female => SpeechVoiceGender.Female,
            SynthesisVoiceGender.Neutral => SpeechVoiceGender.Neutral,
            _ => SpeechVoiceGender.Unknown,
        };
    }
}
