using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Dialpad.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Dialpad.Services;

/// <summary>
/// A telephony provider that controls calls through the Dialpad REST API. It supports both a shared
/// API key and per-user OAuth 2.0 authentication. All call control happens server-side, so the soft
/// phone client never talks to Dialpad directly.
/// </summary>
public sealed class DialpadTelephonyProvider :
    ITelephonyProvider,
    ITelephonyCallControlProvider,
    ITelephonyInboundCallProvider,
    ITelephonyHoldProvider,
    ITelephonyMuteProvider,
    ITelephonyTransferProvider,
    ITelephonyConferenceProvider,
    ITelephonyDtmfProvider,
    ITelephonyVoicemailProvider,
    ITelephonySoftPhoneCredentialsProvider,
    ITelephonyAttendedTransferProvider,
    ITelephonyAudioProvider,
    ITelephonyAuthenticationProvider,
    ITelephonyUserConnectionMetadataProvider,
    ITelephonyCallStateProvider,
    ITelephonyDirectoryProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITelephonyAuthenticationService _authenticationService;
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private readonly DialpadOptions _dialpadOptions;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialpadTelephonyProvider"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="authenticationService">The telephony authentication service used to resolve user tokens.</param>
    /// <param name="clock">The clock used to compute token expiration.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    /// <param name="dialpadOptions">The active Dialpad settings resolved for the tenant shell.</param>
    public DialpadTelephonyProvider(
        IHttpClientFactory httpClientFactory,
        ITelephonyAuthenticationService authenticationService,
        IClock clock,
        ILogger<DialpadTelephonyProvider> logger,
        IStringLocalizer<DialpadTelephonyProvider> stringLocalizer,
        IOptions<DialpadOptions> dialpadOptions)
    {
        _httpClientFactory = httpClientFactory;
        _authenticationService = authenticationService;
        _clock = clock;
        _logger = logger;
        _dialpadOptions = dialpadOptions.Value;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public LocalizedString Name => S["Dialpad"];

    /// <inheritdoc/>
    public string AuthenticationScheme => TelephonyConstants.AuthenticationSchemes.OAuth2;

    /// <inheritdoc/>
    public bool SupportsProofKeyForCodeExchange => true;

    /// <inheritdoc/>
    public TelephonyCapabilities Capabilities
    {
        get
        {
            return TelephonyCapabilities.Dial |
                TelephonyCapabilities.Hangup |
                TelephonyCapabilities.Hold |
                TelephonyCapabilities.Resume |
                TelephonyCapabilities.Mute |
                TelephonyCapabilities.Transfer |
                TelephonyCapabilities.AttendedTransfer |
                TelephonyCapabilities.Merge |
                TelephonyCapabilities.SendDigits |
                TelephonyCapabilities.ReceiveCalls |
                TelephonyCapabilities.Voicemail |
                TelephonyCapabilities.Directory;
        }
    }

    /// <inheritdoc/>
    public TelephonyAudioCapabilities AudioCapabilities => TelephonyAudioCapabilities.ExternalDevice;

    /// <inheritdoc/>
    public TelephonyAudioMode ConfiguredAudioMode => TelephonyAudioMode.ExternalDevice;

    /// <inheritdoc/>
    public string BrowserMediaAdapterName => null;

    /// <inheritdoc/>
    public bool RequiresUserAuthentication
    {
        get
        {
            var settings = _dialpadOptions;

            return settings.GetEffectiveAuthenticationType() == DialpadAuthenticationType.OAuth2 &&
                !string.IsNullOrWhiteSpace(settings.ClientId) &&
                !string.IsNullOrEmpty(settings.ClientSecret);
        }
    }

    /// <inheritdoc/>
    public async Task<TelephonyResult> DialAsync(DialRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.To))
        {
            return TelephonyResult.Failed(S["A destination phone number is required to place a call."].Value);
        }

        var settings = _dialpadOptions;

        if (!IsConfigured(settings))
        {
            return NotConfigured();
        }

        var bearerToken = await GetBearerTokenAsync(settings, cancellationToken);

        if (string.IsNullOrEmpty(bearerToken))
        {
            return NotConnected();
        }

        var userId = await GetDialpadUserIdAsync(settings, bearerToken, cancellationToken);

        if (!userId.HasValue)
        {
            return TelephonyResult.Failed(S["Dialpad could not determine the user placing the call."].Value);
        }

        var callerId = string.IsNullOrWhiteSpace(request.From) ? settings.OutboundCallerId : request.From;

        var body = new Dictionary<string, object>
        {
            ["phone_number"] = request.To,
            ["user_id"] = userId.Value,
        };

        if (!string.IsNullOrWhiteSpace(callerId))
        {
            body["outbound_caller_id"] = callerId;
        }

        try
        {
            var client = CreateClient(settings, bearerToken);

            using var content = JsonContent.Create(body);
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "call")
            {
                Content = content,
            };

            if (request.Metadata?.TryGetValue(
                TelephonyConstants.RequestMetadata.IdempotencyKey,
                out var idempotencyKey) == true &&
                !string.IsNullOrWhiteSpace(idempotencyKey))
            {
                requestMessage.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
            }

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Submitting Dialpad outbound call initiation through the {Environment} environment at {ApiBaseUrl}. AuthenticationType={AuthenticationType}, HasOutboundCallerId={HasOutboundCallerId}. Dialpad will ring the user's active devices before completing the outbound leg.",
                    settings.Environment,
                    settings.ApiBaseUrl,
                    settings.GetEffectiveAuthenticationType(),
                    !string.IsNullOrWhiteSpace(callerId));
            }

            using var response = await client.SendAsync(requestMessage, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorPayload = await SafeReadContentAsync(response, cancellationToken);

                _logger.LogError(
                    "Dialpad rejected a dial request with status code {StatusCode}. Response: {Response}",
                    response.StatusCode,
                    errorPayload.SanitizeLogValue());

                if (TelephonyProviderResponse.IsAmbiguousStatusCode(response.StatusCode))
                {
                    return TelephonyResult.Unknown(S["Dialpad did not confirm whether the call was placed."].Value);
                }

                return TelephonyResult.Failed(S["Dialpad could not place the call."].Value);
            }

            var callId = await ReadCallIdAsync(response, cancellationToken);

            var call = new TelephonyCall
            {
                CallId = callId,
                From = callerId,
                To = request.To,
                State = CallState.Connecting,
                Direction = CallDirection.Outbound,
                ProviderName = DialpadConstants.ProviderTechnicalName,
                StartedUtc = _clock.UtcNow,
            };
            call.Metadata["dialpadInitiationMode"] = "RingAllDevices";
            call.Metadata["requiresActiveDialpadDevice"] = true;

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Dialpad accepted outbound call initiation. CallId={CallId}, InitialState={CallState}.",
                    callId.SanitizeLogValue(),
                    call.State);
            }

            return TelephonyResult.Success(call);
        }

        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            _logger.LogError(ex, "An error occurred while placing a Dialpad call.");

            return TelephonyResult.Unknown(S["Dialpad did not confirm whether the call was placed."].Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while preparing a Dialpad call.");

            return TelephonyResult.Failed(S["Dialpad could not place the call."].Value);
        }
    }

    /// <inheritdoc/>
    public async Task<TelephonyCallLookupResult> GetCallStateAsync(string callId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(callId))
        {
            return new TelephonyCallLookupResult
            {
                Succeeded = false,
                Error = S["A call id is required to query the call state."].Value,
            };
        }

        var settings = _dialpadOptions;

        if (!IsConfigured(settings))
        {
            return new TelephonyCallLookupResult
            {
                Succeeded = false,
                Error = NotConfigured().Error,
            };
        }

        var bearerToken = await GetBearerTokenAsync(settings, cancellationToken);

        if (string.IsNullOrEmpty(bearerToken))
        {
            return new TelephonyCallLookupResult
            {
                Succeeded = false,
                Error = NotConnected().Error,
            };
        }

        try
        {
            var client = CreateClient(settings, bearerToken);
            using var response = await client.GetAsync($"call/{Uri.EscapeDataString(callId)}", cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new TelephonyCallLookupResult
                {
                    Succeeded = true,
                    Found = false,
                };
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Dialpad rejected a call-state lookup for call {CallId} with status code {StatusCode}.", callId.SanitizeLogValue(), response.StatusCode);

                return new TelephonyCallLookupResult
                {
                    Succeeded = false,
                    Error = S["Dialpad could not query the call state."].Value,
                };
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;
            var stateText = ReadString(root, "status") ?? ReadString(root, "state");
            var state = TryMapLookupState(stateText, out var mappedState)
                ? mappedState
                : CallState.Connected;
            var call = BuildCall(
                callId,
                state,
                isMuted: ReadBoolean(root, "is_muted"),
                isOnHold: state == CallState.OnHold,
                direction: TelephonyProviderResponse.ResolveDirection(ReadString(root, "direction")));

            call.From = ReadString(root, "external_number") ?? ReadString(root, "from");
            call.To = ReadString(root, "target") ?? ReadString(root, "internal_number") ?? ReadString(root, "to");
            call.StartedUtc = ReadDateTimeOffset(root, "date_started") ?? ReadDateTimeOffset(root, "date_connected");
            call.Metadata["dialPadStatus"] = stateText ?? string.Empty;

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Dialpad call-state lookup returned status {DialpadStatus} for call {CallId}; mapped state {CallState}.",
                    stateText?.SanitizeLogValue() ?? "(null)",
                    callId.SanitizeLogValue(),
                    call.State);
            }

            return new TelephonyCallLookupResult
            {
                Succeeded = true,
                Found = true,
                Call = call,
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while querying the Dialpad call state for call {CallId}.", callId.SanitizeLogValue());

            return new TelephonyCallLookupResult
            {
                Succeeded = false,
                Error = S["Dialpad could not query the call state."].Value,
            };
        }
    }

    /// <inheritdoc/>
    public Task<TelephonyResult> HangupAsync(CallReference call, CancellationToken cancellationToken = default)
        => ExecuteCallActionAsync(call?.CallId, "hangup", body: null, () => BuildCall(call?.CallId, CallState.Disconnected), cancellationToken);

    /// <inheritdoc/>
    public Task<TelephonyResult> HoldAsync(CallReference call, CancellationToken cancellationToken = default)
        => ExecuteCallActionAsync(call?.CallId, "hold", body: null, () => BuildCall(call?.CallId, CallState.OnHold, isOnHold: true), cancellationToken);

    /// <inheritdoc/>
    public Task<TelephonyResult> ResumeAsync(CallReference call, CancellationToken cancellationToken = default)
        => ExecuteCallActionAsync(call?.CallId, "resume", body: null, () => BuildCall(call?.CallId, CallState.Connected), cancellationToken);

    /// <inheritdoc/>
    public Task<TelephonyResult> MuteAsync(CallReference call, CancellationToken cancellationToken = default)
        => ExecuteCallActionAsync(call?.CallId, "mute", body: null, () => BuildCall(call?.CallId, CallState.Connected, isMuted: true), cancellationToken);

    /// <inheritdoc/>
    public Task<TelephonyResult> UnmuteAsync(CallReference call, CancellationToken cancellationToken = default)
        => ExecuteCallActionAsync(call?.CallId, "unmute", body: null, () => BuildCall(call?.CallId, CallState.Connected), cancellationToken);

    /// <inheritdoc/>
    public Task<TelephonyResult> StartAttendedTransferAsync(TransferRequest request, CancellationToken cancellationToken = default)
        => TransferAsync(request, cancellationToken);

    /// <inheritdoc/>
    public Task<TelephonyResult> TransferAsync(TransferRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.To))
        {
            return Task.FromResult(TelephonyResult.Failed(S["A destination is required to transfer a call."].Value));
        }

        var state = request.Mode == TransferMode.Warm ? CallState.Connected : CallState.Disconnected;

        return ExecuteCallActionAsync(
            request.CallId,
            "transfer",
            new Dictionary<string, object> { ["to"] = request.To },
            () => BuildCall(request.CallId, state),
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<TelephonyResult> MergeAsync(MergeRequest request, CancellationToken cancellationToken = default)
    {
        var callIds = request?.GetCallIds();

        if (callIds is null || callIds.Count < 2)
        {
            return TelephonyResult.Failed(S["At least two calls are required to merge calls."].Value);
        }

        var primaryCallId = callIds[0];

        foreach (var secondaryCallId in callIds.Skip(1))
        {
            var result = await ExecuteCallActionAsync(
                primaryCallId,
                "merge",
                new Dictionary<string, object> { ["target_call_id"] = secondaryCallId },
                () => BuildCall(
                    primaryCallId,
                    CallState.Connected,
                    metadata: new Dictionary<string, object>
                    {
                        ["isConference"] = true,
                        ["participantCount"] = callIds.Count,
                    }),
                cancellationToken);

            if (!result.Succeeded)
            {
                return result;
            }
        }

        return TelephonyResult.Success(BuildCall(
            primaryCallId,
            CallState.Connected,
            metadata: new Dictionary<string, object>
            {
                ["isConference"] = true,
                ["participantCount"] = callIds.Count,
            }));
    }

    /// <inheritdoc/>
    public Task<TelephonyResult> SendDigitsAsync(SendDigitsRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Digits))
        {
            return Task.FromResult(TelephonyResult.Failed(S["Digits are required."].Value));
        }

        return ExecuteCallActionAsync(
            request.CallId,
            "digits",
            new Dictionary<string, object> { ["digits"] = request.Digits },
            () => null,
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task<TelephonyResult> AnswerAsync(CallReference call, CancellationToken cancellationToken = default)
        => ExecuteCallActionAsync(call?.CallId, "answer", body: null, () => BuildCall(call?.CallId, CallState.Connected, direction: CallDirection.Inbound), cancellationToken);

    /// <inheritdoc/>
    public Task<TelephonyResult> RejectAsync(CallReference call, CancellationToken cancellationToken = default)
        => ExecuteCallActionAsync(call?.CallId, "reject", body: null, () => BuildCall(call?.CallId, CallState.Disconnected, direction: CallDirection.Inbound), cancellationToken);

    /// <inheritdoc/>
    public Task<TelephonyResult> SendToVoicemailAsync(CallReference call, CancellationToken cancellationToken = default)
        => ExecuteCallActionAsync(
            call?.CallId,
            "transfer",
            new Dictionary<string, object> { ["to_voicemail"] = true },
            () => BuildCall(call?.CallId, CallState.Disconnected, direction: CallDirection.Inbound),
            cancellationToken);

    /// <inheritdoc/>
    public Task<TelephonyClientCredentials> GetClientCredentialsAsync(CancellationToken cancellationToken = default)
    {
        var settings = _dialpadOptions;

        if (!IsConfigured(settings))
        {
            return Task.FromResult<TelephonyClientCredentials>(null);
        }

        // Dialpad performs all call control server-side, so the browser does not receive an access token.
        return Task.FromResult<TelephonyClientCredentials>(new TelephonyClientCredentials
        {
            ProviderName = DialpadConstants.ProviderTechnicalName,
            Settings = new Dictionary<string, string>(),
        });
    }

    /// <inheritdoc/>
    public async Task<TelephonyDirectoryResult> GetDirectoryAsync(CancellationToken cancellationToken = default)
    {
        var settings = _dialpadOptions;

        if (!IsConfigured(settings))
        {
            return new TelephonyDirectoryResult
            {
                Succeeded = false,
                Error = NotConfigured().Error,
            };
        }

        var bearerToken = await GetBearerTokenAsync(settings, cancellationToken);

        if (string.IsNullOrEmpty(bearerToken))
        {
            return new TelephonyDirectoryResult
            {
                Succeeded = false,
                Error = NotConnected().Error,
            };
        }

        try
        {
            var client = CreateClient(settings, bearerToken);
            var entries = new List<TelephonyDirectoryEntry>();
            var visitedCursors = new HashSet<string>(StringComparer.Ordinal);
            string cursor = null;

            do
            {
                var path = string.IsNullOrWhiteSpace(cursor)
                    ? "users"
                    : QueryHelpers.AddQueryString("users", "cursor", cursor);
                using var response = await client.GetAsync(path, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Dialpad rejected a directory lookup with status code {StatusCode}.", response.StatusCode);

                    return new TelephonyDirectoryResult
                    {
                        Succeeded = false,
                        Error = S["Dialpad could not load the directory."].Value,
                    };
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                var root = document.RootElement;

                if (root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
                {
                    foreach (var user in items.EnumerateArray())
                    {
                        var extension = ReadString(user, "extension");
                        var phoneNumber = ReadString(user, "phone_number");
                        var destination = !string.IsNullOrWhiteSpace(extension) ? extension : phoneNumber;

                        if (string.IsNullOrWhiteSpace(destination))
                        {
                            continue;
                        }

                        var firstName = ReadString(user, "first_name");
                        var lastName = ReadString(user, "last_name");
                        var displayName = string.Join(
                            " ",
                            new[] { firstName, lastName }.Where(value => !string.IsNullOrWhiteSpace(value)));

                        if (string.IsNullOrWhiteSpace(displayName))
                        {
                            displayName = ReadString(user, "email") ?? destination;
                        }

                        entries.Add(new TelephonyDirectoryEntry
                        {
                            Id = ReadScalarString(user, "id") ?? destination,
                            DisplayName = displayName,
                            Destination = destination,
                            Extension = extension,
                            PhoneNumber = phoneNumber,
                            Detail = ReadString(user, "email"),
                        });
                    }
                }

                cursor = ReadString(root, "cursor");

                if (!string.IsNullOrWhiteSpace(cursor) && !visitedCursors.Add(cursor))
                {
                    _logger.LogWarning("Dialpad returned a repeated directory cursor; pagination stopped to avoid a lookup loop.");
                    break;
                }
            }
            while (!string.IsNullOrWhiteSpace(cursor));

            return new TelephonyDirectoryResult
            {
                Succeeded = true,
                Entries = entries
                    .OrderBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while loading the Dialpad directory.");

            return new TelephonyDirectoryResult
            {
                Succeeded = false,
                Error = S["Dialpad could not load the directory."].Value,
            };
        }
    }

    /// <inheritdoc/>
    public Task<string> GetAuthorizationUrlAsync(TelephonyAuthorizationContext context, CancellationToken cancellationToken = default)
    {
        var settings = _dialpadOptions;

        if (settings.GetEffectiveAuthenticationType() != DialpadAuthenticationType.OAuth2 || string.IsNullOrWhiteSpace(settings.ClientId))
        {
            return Task.FromResult<string>(null);
        }

        var parameters = new Dictionary<string, string>
        {
            ["client_id"] = settings.ClientId,
            ["redirect_uri"] = context.RedirectUri,
            ["response_type"] = "code",
            ["state"] = context.State,
        };

        var scope = BuildScope(settings.Scopes);

        if (!string.IsNullOrWhiteSpace(scope))
        {
            parameters["scope"] = scope;
        }

        if (!string.IsNullOrEmpty(context.CodeChallenge))
        {
            parameters["code_challenge"] = context.CodeChallenge;
            parameters["code_challenge_method"] = string.IsNullOrEmpty(context.CodeChallengeMethod) ? "S256" : context.CodeChallengeMethod;
        }

        return Task.FromResult(QueryHelpers.AddQueryString(DialpadConstants.GetAuthorizeUrl(settings.Environment, settings.Host), parameters));
    }

    /// <inheritdoc/>
    public async Task<TelephonyUserTokens> ExchangeCodeAsync(TelephonyCodeExchangeContext context, CancellationToken cancellationToken = default)
    {
        var settings = _dialpadOptions;

        if (settings.GetEffectiveAuthenticationType() != DialpadAuthenticationType.OAuth2)
        {
            _logger.LogWarning("Cannot complete the Dialpad OAuth code exchange because the active environment is not configured for OAuth 2.0 authentication.");

            return null;
        }

        if (string.IsNullOrWhiteSpace(settings.ClientId) || string.IsNullOrEmpty(settings.ClientSecret))
        {
            _logger.LogWarning("Cannot complete the Dialpad OAuth code exchange because the OAuth client id or client secret is unavailable. If a client secret was saved, it may have failed to decrypt with the current data protection keys; re-enter it under Settings > Communication > Telephony > Dialpad.");

            return null;
        }

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = context.Code,
            ["redirect_uri"] = context.RedirectUri,
            ["client_id"] = settings.ClientId,
            ["client_secret"] = settings.ClientSecret,
        };

        if (!string.IsNullOrEmpty(context.CodeVerifier))
        {
            form["code_verifier"] = context.CodeVerifier;
        }

        return await RequestTokensAsync(form, existingRefreshToken: null, settings, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<TelephonyUserTokens> RefreshTokensAsync(TelephonyUserTokens tokens, CancellationToken cancellationToken = default)
    {
        if (tokens is null || string.IsNullOrEmpty(tokens.RefreshToken))
        {
            return null;
        }

        var settings = _dialpadOptions;

        if (settings.GetEffectiveAuthenticationType() != DialpadAuthenticationType.OAuth2 || string.IsNullOrWhiteSpace(settings.ClientId) || string.IsNullOrEmpty(settings.ClientSecret))
        {
            _logger.LogWarning("Cannot refresh Dialpad OAuth tokens because the active environment is not configured for OAuth 2.0 authentication, or the client id or client secret is unavailable.");

            return null;
        }

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = tokens.RefreshToken,
            ["client_id"] = settings.ClientId,
            ["client_secret"] = settings.ClientSecret,
        };

        return await RequestTokensAsync(form, existingRefreshToken: tokens.RefreshToken, settings, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<TelephonyResult> RevokeTokensAsync(TelephonyUserTokens tokens, CancellationToken cancellationToken = default)
    {
        if (tokens is null || string.IsNullOrEmpty(tokens.AccessToken))
        {
            return TelephonyResult.Success();
        }

        // Attempt revocation whenever an access token exists, regardless of the current authentication
        // mode. A tenant that switched from OAuth to API-key authentication can still hold a previously
        // issued per-user OAuth token that must be revoked at Dialpad, so the deauthorize call must not be
        // skipped just because the effective mode is no longer OAuth.
        var settings = _dialpadOptions;

        try
        {
            var client = _httpClientFactory.CreateClient(DialpadConstants.ProviderTechnicalName);

            using var request = new HttpRequestMessage(HttpMethod.Post, DialpadConstants.GetDeauthorizeUrl(settings.Environment, settings.Host))
            {
                Content = new StringContent(string.Empty),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

            using var response = await client.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Dialpad rejected an OAuth token revocation request with status code {StatusCode}.", response.StatusCode);

                // A timeout, throttling, or server-side error cannot prove whether the unsafe deauthorize
                // POST committed, so the outcome is indeterminate rather than a definitive rejection.
                if (TelephonyProviderResponse.IsAmbiguousStatusCode(response.StatusCode))
                {
                    return TelephonyResult.Unknown($"Dialpad did not confirm the token revocation (status code {(int)response.StatusCode}).");
                }

                return TelephonyResult.Failed($"Dialpad rejected the token revocation request with status code {(int)response.StatusCode}.");
            }

            return TelephonyResult.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while revoking Dialpad OAuth tokens.");

            return TelephonyResult.Unknown("The Dialpad token revocation request could not be completed.");
        }
    }

    /// <inheritdoc/>
    public async Task<TelephonyUserTokens> EnrichTokensAsync(TelephonyUserTokens tokens, CancellationToken cancellationToken = default)
    {
        if (tokens is null || string.IsNullOrEmpty(tokens.AccessToken))
        {
            return tokens;
        }

        var settings = _dialpadOptions;

        if (settings.GetEffectiveAuthenticationType() != DialpadAuthenticationType.OAuth2)
        {
            return tokens;
        }

        var profile = await GetCurrentUserProfileAsync(settings, tokens.AccessToken, cancellationToken);

        if (profile is null)
        {
            return tokens;
        }

        return new TelephonyUserTokens
        {
            ProviderName = tokens.ProviderName,
            AccessToken = tokens.AccessToken,
            RefreshToken = tokens.RefreshToken,
            ExpiresUtc = tokens.ExpiresUtc,
            TokenType = tokens.TokenType,
            Scope = tokens.Scope,
            RemoteUserId = profile.Id,
            RemoteUserName = profile.DisplayName,
            RemoteUserEmail = profile.Email,
            RemotePhoneNumber = profile.PhoneNumber,
        };
    }

    private static string BuildScope(string configuredScopes)
    {
        if (string.IsNullOrWhiteSpace(configuredScopes))
        {
            return null;
        }

        var scopes = new List<string>();

        foreach (var scope in configuredScopes.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!scopes.Contains(scope, StringComparer.OrdinalIgnoreCase))
            {
                scopes.Add(scope);
            }
        }

        return string.Join(' ', scopes);
    }

    private async Task<TelephonyUserTokens> RequestTokensAsync(
        Dictionary<string, string> form,
        string existingRefreshToken,
        DialpadOptions settings,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient(DialpadConstants.ProviderTechnicalName);

            using var content = new FormUrlEncodedContent(form);
            using var response = await client.PostAsync(DialpadConstants.GetTokenUrl(settings.Environment, settings.Host), content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorPayload = await SafeReadContentAsync(response, cancellationToken);

                _logger.LogError(
                    "Dialpad rejected an OAuth token request with status code {StatusCode}. Response: {Response}",
                    response.StatusCode,
                    errorPayload.SanitizeLogValue());

                return null;
            }

            var tokens = await ParseTokenResponseAsync(response, cancellationToken);

            if (tokens is not null && string.IsNullOrEmpty(tokens.RefreshToken))
            {
                tokens.RefreshToken = existingRefreshToken;
            }

            return tokens;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while requesting Dialpad OAuth tokens.");

            return null;
        }
    }

    private static async Task<string> SafeReadContentAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return string.Empty;
        }
    }

    private async Task<TelephonyUserTokens> ParseTokenResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("access_token", out var accessTokenElement))
        {
            return null;
        }

        var tokens = new TelephonyUserTokens
        {
            ProviderName = DialpadConstants.ProviderTechnicalName,
            AccessToken = accessTokenElement.GetString(),
            RefreshToken = root.TryGetProperty("refresh_token", out var refreshTokenElement) ? refreshTokenElement.GetString() : null,
            TokenType = root.TryGetProperty("token_type", out var tokenTypeElement) ? tokenTypeElement.GetString() : "Bearer",
            Scope = root.TryGetProperty("scope", out var scopeElement) ? scopeElement.GetString() : null,
        };

        if (root.TryGetProperty("expires_in", out var expiresInElement) && expiresInElement.TryGetInt32(out var seconds))
        {
            tokens.ExpiresUtc = _clock.UtcNow.AddSeconds(seconds);
        }

        return tokens;
    }

    private async Task<string> GetBearerTokenAsync(DialpadOptions dialpadOptions, CancellationToken cancellationToken)
    {
        if (dialpadOptions.GetEffectiveAuthenticationType() == DialpadAuthenticationType.OAuth2)
        {
            var tokens = await _authenticationService.GetValidTokensAsync(DialpadConstants.ProviderTechnicalName, cancellationToken);

            return tokens?.AccessToken;
        }

        return dialpadOptions.ApiToken;
    }

    private async Task<long?> GetDialpadUserIdAsync(
        DialpadOptions settings,
        string bearerToken,
        CancellationToken cancellationToken)
    {
        if (long.TryParse(settings.UserId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var configuredUserId))
        {
            return configuredUserId;
        }

        if (settings.GetEffectiveAuthenticationType() != DialpadAuthenticationType.OAuth2)
        {
            _logger.LogError("The configured Dialpad user id is not a valid integer.");

            return null;
        }

        var profile = await GetCurrentUserProfileAsync(settings, bearerToken, cancellationToken);

        if (profile is null || string.IsNullOrWhiteSpace(profile.Id))
        {
            _logger.LogError("Dialpad returned a current-user response without a valid user id.");

            return null;
        }

        if (long.TryParse(profile.Id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var profileUserId))
        {
            return profileUserId;
        }

        _logger.LogError("Dialpad returned a non-numeric current-user id.");

        return null;
    }

    private async Task<DialpadCurrentUserProfile> GetCurrentUserProfileAsync(
        DialpadOptions settings,
        string bearerToken,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = CreateClient(settings, bearerToken);
            using var response = await client.GetAsync("users/me", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorPayload = await SafeReadContentAsync(response, cancellationToken);

                _logger.LogError(
                    "Dialpad rejected the current-user request with status code {StatusCode}. Response: {Response}",
                    response.StatusCode,
                    errorPayload.SanitizeLogValue());

                return null;
            }

            var payload = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(payload))
            {
                using var document = JsonDocument.Parse(payload);
                var root = document.RootElement;

                if (root.ValueKind == JsonValueKind.Object)
                {
                    var firstName = ReadString(root, "first_name");
                    var lastName = ReadString(root, "last_name");
                    var displayName = ReadString(root, "name");

                    if (string.IsNullOrWhiteSpace(displayName))
                    {
                        displayName = string.Join(
                            " ",
                            new[] { firstName, lastName }.Where(value => !string.IsNullOrWhiteSpace(value)));
                    }

                    return new DialpadCurrentUserProfile
                    {
                        Id = ReadScalarString(root, "id"),
                        Email = ReadString(root, "email"),
                        PhoneNumber = ReadPhoneNumber(root),
                        DisplayName = string.IsNullOrWhiteSpace(displayName)
                            ? ReadString(root, "email")
                            : displayName,
                    };
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Dialpad returned an invalid current-user response.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while resolving the current Dialpad user.");
        }

        return null;
    }

    private async Task<TelephonyResult> ExecuteCallActionAsync(
        string callId,
        string action,
        IDictionary<string, object> body,
        Func<TelephonyCall> onSuccess,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(callId))
        {
            return TelephonyResult.Failed(S["A call identifier is required."].Value);
        }

        var settings = _dialpadOptions;

        if (!IsConfigured(settings))
        {
            return NotConfigured();
        }

        var bearerToken = await GetBearerTokenAsync(settings, cancellationToken);

        if (string.IsNullOrEmpty(bearerToken))
        {
            return NotConnected();
        }

        try
        {
            var client = CreateClient(settings, bearerToken);

            using var content = body is null ? null : JsonContent.Create(body);
            using var response = await client.PostAsync($"call/{callId}/{action}", content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Dialpad rejected the '{Action}' request for call {CallId} with status code {StatusCode}.", action, callId.SanitizeLogValue(), response.StatusCode);

                return TelephonyResult.Failed(S["Dialpad could not complete the requested operation."].Value);
            }

            return TelephonyResult.Success(onSuccess());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while performing the Dialpad '{Action}' operation.", action);

            return TelephonyResult.Failed(S["Dialpad could not complete the requested operation."].Value);
        }
    }

    private TelephonyResult NotConfigured()
        => TelephonyResult.Failed(S["The Dialpad provider is not enabled or fully configured."].Value);

    private TelephonyResult NotConnected()
        => TelephonyResult.Failed(S["Connect your account to Dialpad to place calls."].Value);

    private static bool IsConfigured(DialpadOptions dialpadOptions)
    {
        if (dialpadOptions is null || !dialpadOptions.IsEnabled)
        {
            return false;
        }

        var authenticationType = dialpadOptions.GetEffectiveAuthenticationType();

        if (authenticationType == DialpadAuthenticationType.OAuth2)
        {
            return !string.IsNullOrWhiteSpace(dialpadOptions.ClientId) && !string.IsNullOrEmpty(dialpadOptions.ClientSecret);
        }

        return authenticationType == DialpadAuthenticationType.ApiKey && !string.IsNullOrWhiteSpace(dialpadOptions.ApiToken);
    }

    private static TelephonyCall BuildCall(
        string callId,
        CallState state,
        bool isMuted = false,
        bool isOnHold = false,
        CallDirection direction = CallDirection.Outbound,
        IDictionary<string, object> metadata = null)
    {
        return new TelephonyCall
        {
            CallId = callId,
            State = state,
            IsMuted = isMuted,
            IsOnHold = isOnHold,
            Direction = direction,
            ProviderName = DialpadConstants.ProviderTechnicalName,
            Metadata = metadata ?? new Dictionary<string, object>(),
        };
    }

    private static bool TryMapLookupState(string state, out CallState mapped)
    {
        mapped = state?.Trim().ToLowerInvariant() switch
        {
            "calling" or "dialing" or "connecting" or "preanswer" => CallState.Connecting,
            "ringing" => CallState.Ringing,
            "connected" or "active" => CallState.Connected,
            "hold" or "on_hold" or "parked" => CallState.OnHold,
            "hangup" or "ended" or "disconnected" or "completed" or "voicemail" => CallState.Disconnected,
            "missed" or "no_answer" or "noanswer" => CallState.Failed,
            "rejected" or "declined" or "busy" => CallState.Failed,
            "canceled" or "cancelled" or "abandoned" => CallState.Disconnected,
            _ => (CallState)(-1),
        };

        return Enum.IsDefined(mapped);
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static string ReadScalarString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null,
        };
    }

    private static string ReadPhoneNumber(JsonElement element)
    {
        var phoneNumber = ReadString(element, "phone_number");

        if (!string.IsNullOrWhiteSpace(phoneNumber))
        {
            return phoneNumber;
        }

        if (element.TryGetProperty("phone_numbers", out var phoneNumbers) &&
            phoneNumbers.ValueKind == JsonValueKind.Array)
        {
            foreach (var phone in phoneNumbers.EnumerateArray())
            {
                var value = phone.ValueKind switch
                {
                    JsonValueKind.String => phone.GetString(),
                    JsonValueKind.Object => ReadString(phone, "phone_number") ?? ReadString(phone, "number"),
                    _ => null,
                };

                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return ReadString(element, "extension");
    }

    private static bool ReadBoolean(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return false;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            _ => false,
        };
    }

    internal static DateTimeOffset? ReadDateTimeOffset(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(
                value.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            return parsed;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var unixValue))
        {
            return unixValue > 100_000_000_000
                ? DateTimeOffset.FromUnixTimeMilliseconds(unixValue)
                : DateTimeOffset.FromUnixTimeSeconds(unixValue);
        }

        return null;
    }

    private static async Task<string> ReadCallIdAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(payload))
            {
                using var document = JsonDocument.Parse(payload);
                var root = document.RootElement;

                if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("id", out var idElement))
                    {
                        return idElement.ValueKind == JsonValueKind.Number ? idElement.GetRawText() : idElement.GetString();
                    }

                    if (root.TryGetProperty("call_id", out var callIdElement))
                    {
                        return callIdElement.ValueKind == JsonValueKind.Number ? callIdElement.GetRawText() : callIdElement.GetString();
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Ignore malformed responses and fall back to a generated identifier.
        }

        return Guid.NewGuid().ToString("N");
    }

    private HttpClient CreateClient(DialpadOptions dialpadOptions, string bearerToken)
    {
        var client = _httpClientFactory.CreateClient(DialpadConstants.ProviderTechnicalName);

        var baseUrl = string.IsNullOrWhiteSpace(dialpadOptions.ApiBaseUrl)
            ? DialpadConstants.GetApiBaseUrl(dialpadOptions.Environment, dialpadOptions.Host)
            : dialpadOptions.ApiBaseUrl.EndsWith('/') ? dialpadOptions.ApiBaseUrl : dialpadOptions.ApiBaseUrl + '/';

        client.BaseAddress = new Uri(baseUrl);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        return client;
    }

}
