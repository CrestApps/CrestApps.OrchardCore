using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CrestApps.OrchardCore.DialPad.Models;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.DialPad.Services;

/// <summary>
/// A telephony provider that controls calls through the DialPad REST API. It supports both a shared
/// API key and per-user OAuth 2.0 authentication. All call control happens server-side, so the soft
/// phone client never talks to DialPad directly.
/// </summary>
public sealed class DialPadTelephonyProvider : ITelephonyProvider, ITelephonyAudioProvider, ITelephonyAuthenticationProvider, ITelephonyCallStateProvider, ITelephonyDirectoryProvider
{
    private readonly ISiteService _siteService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITelephonyAuthenticationService _authenticationService;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    private DialPadSettings _settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialPadTelephonyProvider"/> class.
    /// </summary>
    /// <param name="siteService">The site service used to read DialPad settings.</param>
    /// <param name="dataProtectionProvider">The data protection provider used to unprotect secrets.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="authenticationService">The telephony authentication service used to resolve user tokens.</param>
    /// <param name="clock">The clock used to compute token expiration.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DialPadTelephonyProvider(
        ISiteService siteService,
        IDataProtectionProvider dataProtectionProvider,
        IHttpClientFactory httpClientFactory,
        ITelephonyAuthenticationService authenticationService,
        IClock clock,
        ILogger<DialPadTelephonyProvider> logger,
        IStringLocalizer<DialPadTelephonyProvider> stringLocalizer)
    {
        _siteService = siteService;
        _dataProtectionProvider = dataProtectionProvider;
        _httpClientFactory = httpClientFactory;
        _authenticationService = authenticationService;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public LocalizedString Name => S["DialPad"];

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
            var settings = _siteService.GetSettings<DialPadSettings>();

            return GetEffectiveAuthenticationType(settings) == DialPadAuthenticationType.OAuth2 &&
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

        var settings = await GetResolvedSettingsAsync();

        if (!IsConfigured(settings))
        {
            return NotConfigured();
        }

        var bearerToken = await GetBearerTokenAsync(settings, cancellationToken);

        if (string.IsNullOrEmpty(bearerToken))
        {
            return NotConnected();
        }

        var callerId = string.IsNullOrWhiteSpace(request.From) ? settings.OutboundCallerId : request.From;

        var body = new Dictionary<string, object>
        {
            ["phone_number"] = request.To,
        };

        if (!string.IsNullOrWhiteSpace(callerId))
        {
            body["outbound_caller_id"] = callerId;
        }

        if (!string.IsNullOrWhiteSpace(settings.UserId))
        {
            body["user_id"] = settings.UserId;
        }

        try
        {
            var client = CreateClient(settings, bearerToken);

            using var content = JsonContent.Create(body);
            using var response = await client.PostAsync("call", content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("DialPad rejected a dial request with status code {StatusCode}.", response.StatusCode);

                return TelephonyResult.Failed(S["DialPad could not place the call."].Value);
            }

            var callId = await ReadCallIdAsync(response, cancellationToken);

            var call = new TelephonyCall
            {
                CallId = callId,
                From = callerId,
                To = request.To,
                State = CallState.Connecting,
                Direction = CallDirection.Outbound,
                ProviderName = DialPadConstants.ProviderTechnicalName,
                StartedUtc = _clock.UtcNow,
            };

            return TelephonyResult.Success(call);
        }
        catch (Exception ex)
        {
            _logger.LogError(OperationalLogRedactor.RedactException(ex), "An error occurred while placing a DialPad call.");

            return TelephonyResult.Failed(S["DialPad could not place the call."].Value);
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

        var settings = await GetResolvedSettingsAsync();

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
                _logger.LogError("DialPad rejected a call-state lookup for call {CallId} with status code {StatusCode}.", OperationalLogRedactor.Pseudonymize(callId, OperationalLogIdentifierCategory.Call), response.StatusCode);

                return new TelephonyCallLookupResult
                {
                    Succeeded = false,
                    Error = S["DialPad could not query the call state."].Value,
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
                direction: ResolveDirection(ReadString(root, "direction")));

            call.From = ReadString(root, "external_number") ?? ReadString(root, "from");
            call.To = ReadString(root, "target") ?? ReadString(root, "internal_number") ?? ReadString(root, "to");
            call.StartedUtc = ReadDateTimeOffset(root, "date_started") ?? ReadDateTimeOffset(root, "date_connected");
            call.Metadata["dialPadStatus"] = stateText ?? string.Empty;

            return new TelephonyCallLookupResult
            {
                Succeeded = true,
                Found = true,
                Call = call,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(OperationalLogRedactor.RedactException(ex), "An error occurred while querying the DialPad call state for call {CallId}.", OperationalLogRedactor.Pseudonymize(callId, OperationalLogIdentifierCategory.Call));

            return new TelephonyCallLookupResult
            {
                Succeeded = false,
                Error = S["DialPad could not query the call state."].Value,
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
    public async Task<TelephonyClientCredentials> GetClientCredentialsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetResolvedSettingsAsync();

        if (!IsConfigured(settings))
        {
            return null;
        }

        // DialPad performs all call control server-side, so the browser does not receive an access token.
        return new TelephonyClientCredentials
        {
            ProviderName = DialPadConstants.ProviderTechnicalName,
            Settings = new Dictionary<string, string>(),
        };
    }

    /// <inheritdoc/>
    public async Task<TelephonyDirectoryResult> GetDirectoryAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetResolvedSettingsAsync();

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
                    _logger.LogError("DialPad rejected a directory lookup with status code {StatusCode}.", response.StatusCode);

                    return new TelephonyDirectoryResult
                    {
                        Succeeded = false,
                        Error = S["DialPad could not load the directory."].Value,
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
                    _logger.LogWarning("DialPad returned a repeated directory cursor; pagination stopped to avoid a lookup loop.");
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
        catch (Exception ex)
        {
            _logger.LogError(OperationalLogRedactor.RedactException(ex), "An error occurred while loading the DialPad directory.");

            return new TelephonyDirectoryResult
            {
                Succeeded = false,
                Error = S["DialPad could not load the directory."].Value,
            };
        }
    }

    /// <inheritdoc/>
    public async Task<string> GetAuthorizationUrlAsync(TelephonyAuthorizationContext context, CancellationToken cancellationToken = default)
    {
        var settings = await GetResolvedSettingsAsync();

        if (GetEffectiveAuthenticationType(settings) != DialPadAuthenticationType.OAuth2 || string.IsNullOrWhiteSpace(settings.ClientId))
        {
            return null;
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

        return QueryHelpers.AddQueryString(DialPadConstants.GetAuthorizeUrl(settings.Environment), parameters);
    }

    /// <inheritdoc/>
    public async Task<TelephonyUserTokens> ExchangeCodeAsync(TelephonyCodeExchangeContext context, CancellationToken cancellationToken = default)
    {
        var settings = await GetResolvedSettingsAsync();

        if (GetEffectiveAuthenticationType(settings) != DialPadAuthenticationType.OAuth2 || string.IsNullOrWhiteSpace(settings.ClientId) || string.IsNullOrEmpty(settings.ClientSecret))
        {
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

        var settings = await GetResolvedSettingsAsync();

        if (GetEffectiveAuthenticationType(settings) != DialPadAuthenticationType.OAuth2 || string.IsNullOrWhiteSpace(settings.ClientId) || string.IsNullOrEmpty(settings.ClientSecret))
        {
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
    public async Task RevokeTokensAsync(TelephonyUserTokens tokens, CancellationToken cancellationToken = default)
    {
        if (tokens is null || string.IsNullOrEmpty(tokens.AccessToken))
        {
            return;
        }

        var settings = await GetResolvedSettingsAsync();

        if (GetEffectiveAuthenticationType(settings) != DialPadAuthenticationType.OAuth2)
        {
            return;
        }

        try
        {
            var client = _httpClientFactory.CreateClient(DialPadConstants.ProviderTechnicalName);

            using var request = new HttpRequestMessage(HttpMethod.Post, DialPadConstants.GetDeauthorizeUrl(settings.Environment))
            {
                Content = new StringContent(string.Empty),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

            using var response = await client.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("DialPad rejected an OAuth token revocation request with status code {StatusCode}.", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(OperationalLogRedactor.RedactException(ex), "An error occurred while revoking DialPad OAuth tokens.");
        }
    }

    private static string BuildScope(string configuredScopes)
    {
        var scopes = new List<string>();

        if (!string.IsNullOrWhiteSpace(configuredScopes))
        {
            foreach (var scope in configuredScopes.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!scopes.Contains(scope, StringComparer.OrdinalIgnoreCase))
                {
                    scopes.Add(scope);
                }
            }
        }

        if (!scopes.Contains(DialPadConstants.OfflineAccessScope, StringComparer.OrdinalIgnoreCase))
        {
            scopes.Add(DialPadConstants.OfflineAccessScope);
        }

        return string.Join(' ', scopes);
    }

    private async Task<TelephonyUserTokens> RequestTokensAsync(
        Dictionary<string, string> form,
        string existingRefreshToken,
        DialPadSettings settings,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient(DialPadConstants.ProviderTechnicalName);

            using var content = new FormUrlEncodedContent(form);
            using var response = await client.PostAsync(DialPadConstants.GetTokenUrl(settings.Environment), content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("DialPad rejected an OAuth token request with status code {StatusCode}.", response.StatusCode);

                return null;
            }

            var tokens = await ParseTokenResponseAsync(response, cancellationToken);

            if (tokens is not null && string.IsNullOrEmpty(tokens.RefreshToken))
            {
                tokens.RefreshToken = existingRefreshToken;
            }

            return tokens;
        }
        catch (Exception ex)
        {
            _logger.LogError(OperationalLogRedactor.RedactException(ex), "An error occurred while requesting DialPad OAuth tokens.");

            return null;
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
            ProviderName = DialPadConstants.ProviderTechnicalName,
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

    private async Task<string> GetBearerTokenAsync(DialPadSettings settings, CancellationToken cancellationToken)
    {
        if (GetEffectiveAuthenticationType(settings) == DialPadAuthenticationType.OAuth2)
        {
            var tokens = await _authenticationService.GetValidTokensAsync(DialPadConstants.ProviderTechnicalName, cancellationToken);

            return tokens?.AccessToken;
        }

        return settings.ApiToken;
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

        var settings = await GetResolvedSettingsAsync();

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
                _logger.LogError("DialPad rejected the '{Action}' request for call {CallId} with status code {StatusCode}.", action, OperationalLogRedactor.Pseudonymize(callId, OperationalLogIdentifierCategory.Call), response.StatusCode);

                return TelephonyResult.Failed(S["DialPad could not complete the requested operation."].Value);
            }

            return TelephonyResult.Success(onSuccess());
        }
        catch (Exception ex)
        {
            _logger.LogError(OperationalLogRedactor.RedactException(ex), "An error occurred while performing the DialPad '{Action}' operation.", action);

            return TelephonyResult.Failed(S["DialPad could not complete the requested operation."].Value);
        }
    }

    private TelephonyResult NotConfigured()
        => TelephonyResult.Failed(S["The DialPad provider is not enabled or fully configured."].Value);

    private TelephonyResult NotConnected()
        => TelephonyResult.Failed(S["Connect your account to DialPad to place calls."].Value);

    private static bool IsConfigured(DialPadSettings settings)
    {
        if (settings is null || !settings.IsEnabled)
        {
            return false;
        }

        var authenticationType = GetEffectiveAuthenticationType(settings);

        if (authenticationType == DialPadAuthenticationType.OAuth2)
        {
            return !string.IsNullOrWhiteSpace(settings.ClientId) && !string.IsNullOrEmpty(settings.ClientSecret);
        }

        return authenticationType == DialPadAuthenticationType.ApiKey && !string.IsNullOrWhiteSpace(settings.ApiToken);
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
            ProviderName = DialPadConstants.ProviderTechnicalName,
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

    private static CallDirection ResolveDirection(string direction)
    {
        return string.Equals(direction?.Trim(), "inbound", StringComparison.OrdinalIgnoreCase)
            ? CallDirection.Inbound
            : CallDirection.Outbound;
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

    private static DateTimeOffset? ReadDateTimeOffset(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(value.GetString(), out var parsed))
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

    private static DialPadAuthenticationType GetEffectiveAuthenticationType(DialPadSettings settings)
    {
        if (settings.AuthenticationType != DialPadAuthenticationType.NotConfigured)
        {
            return settings.AuthenticationType;
        }

        if (!string.IsNullOrEmpty(settings.ApiToken))
        {
            return DialPadAuthenticationType.ApiKey;
        }

        if (!string.IsNullOrWhiteSpace(settings.ClientId) || !string.IsNullOrEmpty(settings.ClientSecret))
        {
            return DialPadAuthenticationType.OAuth2;
        }

        return DialPadAuthenticationType.NotConfigured;
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

    private HttpClient CreateClient(DialPadSettings settings, string bearerToken)
    {
        var client = _httpClientFactory.CreateClient(DialPadConstants.ProviderTechnicalName);

        var baseUrl = string.IsNullOrWhiteSpace(settings.ApiBaseUrl)
            ? DialPadConstants.GetApiBaseUrl(settings.Environment)
            : settings.ApiBaseUrl.EndsWith('/') ? settings.ApiBaseUrl : settings.ApiBaseUrl + '/';

        client.BaseAddress = new Uri(baseUrl);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        return client;
    }

    private async Task<DialPadSettings> GetResolvedSettingsAsync()
    {
        if (_settings is null)
        {
            var settings = await _siteService.GetSettingsAsync<DialPadSettings>();
            var apiTokenProtector = _dataProtectionProvider.CreateProtector(DialPadConstants.ProtectorName);
            var clientSecretProtector = _dataProtectionProvider.CreateProtector(DialPadConstants.OAuthProtectorName);

            _settings = new DialPadSettings
            {
                IsEnabled = settings.IsEnabled,
                Environment = settings.Environment,
                ApiBaseUrl = settings.ApiBaseUrl,
                UserId = settings.UserId,
                OutboundCallerId = settings.OutboundCallerId,
                ApiToken = string.IsNullOrEmpty(settings.ApiToken) ? null : Unprotect(apiTokenProtector, settings.ApiToken),
                AuthenticationType = settings.AuthenticationType,
                ClientId = settings.ClientId,
                ClientSecret = string.IsNullOrEmpty(settings.ClientSecret) ? null : Unprotect(clientSecretProtector, settings.ClientSecret),
                Scopes = settings.Scopes,
            };
        }

        return _settings;
    }

    private string Unprotect(IDataProtector protector, string value)
    {
        try
        {
            return protector.Unprotect(value);
        }
        catch (Exception ex)
        {
            _logger.LogError(OperationalLogRedactor.RedactException(ex), "Unable to unprotect a DialPad secret.");

            return null;
        }
    }
}
