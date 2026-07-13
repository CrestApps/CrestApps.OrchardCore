using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Asterisk.Services;

internal abstract class AsteriskTelephonyProviderBase : ITelephonyProvider, ITelephonyCallStateProvider, ITelephonyDirectoryProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    protected AsteriskTelephonyProviderBase(
        IHttpClientFactory httpClientFactory,
        IClock clock,
        ILogger logger,
        IStringLocalizer stringLocalizer)
    {
        _httpClientFactory = httpClientFactory;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    protected IStringLocalizer S { get; }

    protected abstract string ProviderName { get; }

    public abstract LocalizedString Name { get; }

    public virtual TelephonyCapabilities Capabilities
        => GetCapabilities(null, false);

    public async Task<TelephonyResult> DialAsync(DialRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.To))
        {
            return TelephonyResult.Failed(S["A destination phone number or endpoint is required to place a call."].Value);
        }

        var settings = await GetResolvedSettingsAsync(cancellationToken);

        if (!IsConfigured(settings))
        {
            return NotConfigured();
        }

        var endpoint = AsteriskSettingsUtilities.ResolveEndpoint(settings.EndpointTemplate, request.To);

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return TelephonyResult.Failed(S["The configured endpoint template did not produce a valid Asterisk endpoint."].Value);
        }

        var callerId = string.IsNullOrWhiteSpace(request.From)
            ? settings.OutboundCallerId
            : request.From;

        var query = CreateDialQuery(settings, endpoint);

        if (!string.IsNullOrWhiteSpace(callerId))
        {
            query["callerId"] = callerId;
        }

        try
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                var dialMode = query.ContainsKey("app") ? "Stasis" : "Dialplan";

                _logger.LogInformation(
                    "Sending an Asterisk dial request for provider {ProviderName}. Endpoint: {Endpoint}. Mode: {DialMode}.",
                    ProviderName,
                    endpoint,
                    dialMode);
            }

            using var response = await SendAsync(settings, HttpMethod.Post, "channels", query, request.Metadata, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await ReadResponseBodyAsync(response, cancellationToken);

                _logger.LogError(
                    "Asterisk rejected a dial request for provider {ProviderName} with status code {StatusCode}. Response: {ResponseBody}",
                    ProviderName,
                    response.StatusCode,
                    responseBody);

                return TelephonyResult.Failed(S["The call could not be placed."].Value);
            }

            var callId = await ReadIdAsync(response, cancellationToken);

            var call = new TelephonyCall
            {
                CallId = callId ?? request.To,
                From = callerId,
                To = request.To,
                State = CallState.Connecting,
                Direction = CallDirection.Outbound,
                ProviderName = ProviderName,
                StartedUtc = _clock.UtcNow,
                Metadata = request.Metadata?.ToDictionary(
                    entry => entry.Key,
                    entry => (object)entry.Value,
                    StringComparer.OrdinalIgnoreCase) ?? [],
            };

            return TelephonyResult.Success(call);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while placing an Asterisk call for provider {ProviderName}.", ProviderName);

            return TelephonyResult.Failed(S["The call could not be placed."].Value);
        }
    }

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

        var settings = await GetResolvedSettingsAsync(cancellationToken);

        if (!IsConfigured(settings))
        {
            return new TelephonyCallLookupResult
            {
                Succeeded = false,
                Error = NotConfigured().Error,
            };
        }

        try
        {
            using var response = await SendAsync(
                settings,
                HttpMethod.Get,
                $"channels/{Uri.EscapeDataString(callId)}",
                null,
                null,
                cancellationToken);

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
                var responseBody = await ReadResponseBodyAsync(response, cancellationToken);

                _logger.LogError(
                    "Asterisk rejected a call-state lookup for provider {ProviderName}. CallId: {CallId}. Status code: {StatusCode}. Response: {ResponseBody}",
                    ProviderName,
                    callId,
                    response.StatusCode,
                    responseBody);

                return new TelephonyCallLookupResult
                {
                    Succeeded = false,
                    Error = S["Asterisk could not query the call state."].Value,
                };
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;
            var stateText = ReadString(root, "state");

            if (!TryMapLookupState(stateText, out var state))
            {
                _logger.LogWarning(
                    "Asterisk returned an unrecognized channel state '{State}' for provider {ProviderName} call {CallId}; reconciliation was skipped.",
                    stateText,
                    ProviderName,
                    callId);

                return new TelephonyCallLookupResult
                {
                    Succeeded = false,
                    Error = S["Asterisk returned an unrecognized call state."].Value,
                };
            }

            var holdState = await GetChannelBooleanVariableAsync(
                settings,
                callId,
                AsteriskConstants.HoldStateVariableName,
                cancellationToken);
            var muteState = await GetChannelBooleanVariableAsync(
                settings,
                callId,
                AsteriskConstants.MuteStateVariableName,
                cancellationToken);
            var conferenceBridgeId = await GetChannelVariableAsync(
                settings,
                callId,
                AsteriskConstants.ConferenceBridgeVariableName,
                cancellationToken);

            using var verificationResponse = await SendAsync(
                settings,
                HttpMethod.Get,
                $"channels/{Uri.EscapeDataString(callId)}",
                null,
                null,
                cancellationToken);

            if (verificationResponse.StatusCode == HttpStatusCode.NotFound)
            {
                return new TelephonyCallLookupResult
                {
                    Succeeded = true,
                    Found = false,
                };
            }

            if (!verificationResponse.IsSuccessStatusCode)
            {
                var responseBody = await ReadResponseBodyAsync(verificationResponse, cancellationToken);

                _logger.LogError(
                    "Asterisk could not verify a call-state lookup for provider {ProviderName}. CallId: {CallId}. Status code: {StatusCode}. Response: {ResponseBody}",
                    ProviderName,
                    callId,
                    verificationResponse.StatusCode,
                    responseBody);

                return new TelephonyCallLookupResult
                {
                    Succeeded = false,
                    Error = S["Asterisk could not query the call state."].Value,
                };
            }

            var isOnHold = state == CallState.OnHold || holdState == true;

            if (isOnHold)
            {
                state = CallState.OnHold;
            }

            var call = BuildCall(
                callId,
                state,
                isOnHold: isOnHold,
                isMuted: muteState ?? false,
                direction: ResolveDirection(ReadString(root, "direction")));

            call.From = ReadNestedString(root, "caller", "number");
            call.To = ReadNestedString(root, "connected", "number") ?? ReadNestedString(root, "dialplan", "exten");
            call.StartedUtc = _clock.UtcNow;
            call.Metadata["asteriskState"] = stateText ?? string.Empty;

            if (holdState.HasValue)
            {
                call.Metadata["asteriskHoldState"] = holdState.Value;
            }

            if (muteState.HasValue)
            {
                call.Metadata["asteriskMuteState"] = muteState.Value;
            }

            if (!string.IsNullOrWhiteSpace(conferenceBridgeId))
            {
                call.Metadata["isConference"] = true;
                call.Metadata["conferenceBridgeId"] = conferenceBridgeId;
            }

            return new TelephonyCallLookupResult
            {
                Succeeded = true,
                Found = true,
                Call = call,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while querying the Asterisk call state for provider {ProviderName}.", ProviderName);

            return new TelephonyCallLookupResult
            {
                Succeeded = false,
                Error = S["Asterisk could not query the call state."].Value,
            };
        }
    }

    public async Task<TelephonyResult> HangupAsync(CallReference call, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(call?.CallId))
        {
            return TelephonyResult.Failed(S["A call id is required to end the call."].Value);
        }

        var settings = await GetResolvedSettingsAsync(cancellationToken);

        if (!IsConfigured(settings))
        {
            return NotConfigured();
        }

        var bridgeId = await GetChannelVariableAsync(
            settings,
            call.CallId,
            AsteriskConstants.ConferenceBridgeVariableName,
            cancellationToken);
        var result = await ExecuteCallActionAsync(
            call.CallId,
            HttpMethod.Delete,
            "channels/{callId}",
            null,
            null,
            () => BuildCall(call?.CallId, CallState.Disconnected, metadata: call?.Metadata),
            S["The call could not be ended."].Value,
            S["A call id is required to end the call."].Value,
            cancellationToken,
            succeedWhenChannelIsMissing: true);

        if (result.Succeeded && !string.IsNullOrWhiteSpace(bridgeId))
        {
            await DeleteConferenceBridgeWhenEmptyAsync(settings, bridgeId, cancellationToken);
        }

        return result;
    }

    public Task<TelephonyResult> HoldAsync(CallReference call, CancellationToken cancellationToken = default)
        => ExecuteCallActionAsync(
            call?.CallId,
            HttpMethod.Post,
            "channels/{callId}/hold",
            null,
            new Dictionary<string, string> { [AsteriskConstants.HoldStateVariableName] = bool.TrueString },
            () => BuildCall(call?.CallId, CallState.OnHold, isOnHold: true, metadata: call?.Metadata),
            S["The call could not be placed on hold."].Value,
            S["A call id is required to hold the call."].Value,
            cancellationToken);

    public Task<TelephonyResult> ResumeAsync(CallReference call, CancellationToken cancellationToken = default)
        => ExecuteCallActionAsync(
            call?.CallId,
            HttpMethod.Delete,
            "channels/{callId}/hold",
            null,
            new Dictionary<string, string> { [AsteriskConstants.HoldStateVariableName] = bool.FalseString },
            () => BuildCall(call?.CallId, CallState.Connected, metadata: call?.Metadata),
            S["The call could not be resumed."].Value,
            S["A call id is required to resume the call."].Value,
            cancellationToken);

    public Task<TelephonyResult> MuteAsync(CallReference call, CancellationToken cancellationToken = default)
        => ExecuteCallActionAsync(
            call?.CallId,
            HttpMethod.Post,
            "channels/{callId}/mute",
            new Dictionary<string, string> { ["direction"] = "both" },
            new Dictionary<string, string> { [AsteriskConstants.MuteStateVariableName] = bool.TrueString },
            () => BuildCall(call?.CallId, CallState.Connected, isMuted: true, metadata: call?.Metadata),
            S["The call could not be muted."].Value,
            S["A call id is required to mute the call."].Value,
            cancellationToken);

    public Task<TelephonyResult> UnmuteAsync(CallReference call, CancellationToken cancellationToken = default)
        => ExecuteCallActionAsync(
            call?.CallId,
            HttpMethod.Delete,
            "channels/{callId}/mute",
            new Dictionary<string, string> { ["direction"] = "both" },
            new Dictionary<string, string> { [AsteriskConstants.MuteStateVariableName] = bool.FalseString },
            () => BuildCall(call?.CallId, CallState.Connected, metadata: call?.Metadata),
            S["The call could not be unmuted."].Value,
            S["A call id is required to unmute the call."].Value,
            cancellationToken);

    public async Task<TelephonyResult> TransferAsync(TransferRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.CallId) || string.IsNullOrWhiteSpace(request.To))
        {
            return TelephonyResult.Failed(S["A call id and destination are required to transfer the call."].Value);
        }

        if (request.Mode == TransferMode.Warm)
        {
            return TelephonyResult.Failed(S["Warm transfers are not supported."].Value);
        }

        var settings = await GetResolvedSettingsAsync(cancellationToken);

        if (!IsConfigured(settings))
        {
            return NotConfigured();
        }

        var endpoint = AsteriskSettingsUtilities.ResolveEndpoint(settings.EndpointTemplate, request.To);

        if (!AsteriskSettingsUtilities.TryGetImmediateConnectionRoute(endpoint, out var extension, out var context))
        {
            return TelephonyResult.Failed(S["Blind transfers require a Local endpoint template such as Local/{number}@default."].Value);
        }

        return await ExecuteCallActionAsync(
            request.CallId,
            HttpMethod.Post,
            "channels/{callId}/continue",
            new Dictionary<string, string>
            {
                ["context"] = context,
                ["extension"] = extension,
                ["priority"] = "1",
            },
            null,
            () => BuildCall(request.CallId, CallState.Disconnected),
            S["The call could not be transferred."].Value,
            S["A call id is required to transfer the call."].Value,
            cancellationToken);
    }

    public async Task<TelephonyResult> MergeAsync(MergeRequest request, CancellationToken cancellationToken = default)
    {
        var callIds = request?.GetCallIds();

        if (callIds is null || callIds.Count < 2)
        {
            return TelephonyResult.Failed(S["At least two call ids are required to merge calls."].Value);
        }

        var settings = await GetResolvedSettingsAsync(cancellationToken);

        if (!IsConfigured(settings))
        {
            return NotConfigured();
        }

        try
        {
            var createBridgeQuery = new Dictionary<string, string>
            {
                ["type"] = "mixing",
            };

            if (!string.IsNullOrWhiteSpace(request.ConferenceName))
            {
                createBridgeQuery["name"] = request.ConferenceName;
            }

            using var createBridgeResponse = await SendAsync(settings, HttpMethod.Post, "bridges", createBridgeQuery, null, cancellationToken);

            if (!createBridgeResponse.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Asterisk rejected a bridge creation request for provider {ProviderName} with status code {StatusCode}. Response: {ResponseBody}",
                    ProviderName,
                    createBridgeResponse.StatusCode,
                    await ReadResponseBodyAsync(createBridgeResponse, cancellationToken));

                return TelephonyResult.Failed(S["The calls could not be merged."].Value);
            }

            var bridgeId = await ReadIdAsync(createBridgeResponse, cancellationToken);

            if (string.IsNullOrWhiteSpace(bridgeId))
            {
                _logger.LogError("Asterisk did not return a bridge id when merging calls for provider {ProviderName}.", ProviderName);

                return TelephonyResult.Failed(S["The calls could not be merged."].Value);
            }

            var addChannelQuery = new Dictionary<string, string>
            {
                ["channel"] = string.Join(',', callIds),
            };

            using var addChannelResponse = await SendAsync(
                settings,
                HttpMethod.Post,
                $"bridges/{Uri.EscapeDataString(bridgeId)}/addChannel",
                addChannelQuery,
                null,
                cancellationToken);

            if (!addChannelResponse.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Asterisk rejected a bridge add-channel request for provider {ProviderName} with status code {StatusCode}. Response: {ResponseBody}",
                    ProviderName,
                    addChannelResponse.StatusCode,
                    await ReadResponseBodyAsync(addChannelResponse, cancellationToken));

                await DeleteBridgeAsync(settings, bridgeId, cancellationToken);

                return TelephonyResult.Failed(S["The calls could not be merged."].Value);
            }

            var allChannelsTracked = true;

            foreach (var callId in callIds)
            {
                var tracked = await SetChannelVariableAsync(
                    settings,
                    callId,
                    AsteriskConstants.ConferenceBridgeVariableName,
                    bridgeId,
                    cancellationToken);
                var holdCleared = await SetChannelVariableAsync(
                    settings,
                    callId,
                    AsteriskConstants.HoldStateVariableName,
                    bool.FalseString,
                    cancellationToken);

                allChannelsTracked &= tracked && holdCleared;
            }

            if (!allChannelsTracked)
            {
                _logger.LogWarning(
                    "Asterisk merged calls for provider {ProviderName}, but conference state tracking could not be stored on every channel. BridgeId: {BridgeId}.",
                    ProviderName,
                    bridgeId);
            }

            return TelephonyResult.Success(BuildCall(
                callIds[0],
                CallState.Connected,
                metadata: new Dictionary<string, object>
                {
                    ["isConference"] = true,
                    ["conferenceBridgeId"] = bridgeId,
                    ["participantCount"] = callIds.Count,
                }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while merging Asterisk calls for provider {ProviderName}.", ProviderName);

            return TelephonyResult.Failed(S["The calls could not be merged."].Value);
        }
    }

    public Task<TelephonyResult> SendDigitsAsync(SendDigitsRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Digits))
        {
            return Task.FromResult(TelephonyResult.Failed(S["Digits are required."].Value));
        }

        return ExecuteCallActionAsync(
            request.CallId,
            HttpMethod.Post,
            "channels/{callId}/dtmf",
            new Dictionary<string, string> { ["dtmf"] = request.Digits },
            null,
            () => null,
            S["The digits could not be sent."].Value,
            S["A call id is required to send digits."].Value,
            cancellationToken);
    }

    public Task<TelephonyResult> AnswerAsync(CallReference call, CancellationToken cancellationToken = default)
        => ExecuteCallActionAsync(
            call?.CallId,
            HttpMethod.Post,
            "channels/{callId}/answer",
            null,
            null,
            () => BuildCall(call?.CallId, CallState.Connected, direction: CallDirection.Inbound, metadata: call?.Metadata),
            S["The call could not be answered."].Value,
            S["A call id is required to answer the call."].Value,
            cancellationToken);

    public Task<TelephonyResult> RejectAsync(CallReference call, CancellationToken cancellationToken = default)
        => ExecuteCallActionAsync(
            call?.CallId,
            HttpMethod.Delete,
            "channels/{callId}",
            null,
            null,
            () => BuildCall(call?.CallId, CallState.Disconnected, direction: CallDirection.Inbound, metadata: call?.Metadata),
            S["The call could not be rejected."].Value,
            S["A call id is required to reject the call."].Value,
            cancellationToken);

    public async Task<TelephonyResult> SendToVoicemailAsync(CallReference call, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(call?.CallId))
        {
            return TelephonyResult.Failed(S["A call id is required to send the call to voicemail."].Value);
        }

        var settings = await GetResolvedSettingsAsync(cancellationToken);

        if (!IsConfigured(settings))
        {
            return NotConfigured();
        }

        if (!AsteriskSettingsUtilities.HasVoicemailConfiguration(settings))
        {
            return TelephonyResult.Failed(S["Voicemail is not configured for the current telephony provider."].Value);
        }

        var extension = ResolveVoicemailExtension(settings.VoicemailExtensionTemplate, call);

        if (string.IsNullOrWhiteSpace(extension))
        {
            return TelephonyResult.Failed(S["The call does not contain enough metadata to resolve the voicemail destination."].Value);
        }

        try
        {
            await SetChannelVariablesAsync(settings, call.CallId, call.Metadata, cancellationToken);

            return await ExecuteCallActionAsync(
                call.CallId,
                HttpMethod.Post,
                "channels/{callId}/continue",
                new Dictionary<string, string>
                {
                    ["context"] = settings.VoicemailContext,
                    ["extension"] = extension,
                    ["priority"] = AsteriskSettingsUtilities.ToInvariantString(settings.VoicemailPriority),
                },
                null,
                () => BuildCall(call.CallId, CallState.Disconnected, direction: CallDirection.Inbound, metadata: call.Metadata),
                S["The call could not be sent to voicemail."].Value,
                S["A call id is required to send the call to voicemail."].Value,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while routing an Asterisk call to voicemail for provider {ProviderName}.", ProviderName);

            return TelephonyResult.Failed(S["The call could not be sent to voicemail."].Value);
        }
    }

    public async Task<TelephonyClientCredentials> GetClientCredentialsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetResolvedSettingsAsync(cancellationToken);

        if (!IsConfigured(settings))
        {
            return null;
        }

        return new TelephonyClientCredentials
        {
            ProviderName = ProviderName,
            Settings = new Dictionary<string, string>(),
        };
    }

    public async Task<TelephonyDirectoryResult> GetDirectoryAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetResolvedSettingsAsync(cancellationToken);

        if (!IsConfigured(settings))
        {
            return new TelephonyDirectoryResult
            {
                Succeeded = false,
                Error = NotConfigured().Error,
            };
        }

        try
        {
            using var response = await SendAsync(settings, HttpMethod.Get, "endpoints", null, null, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Asterisk rejected a directory lookup for provider {ProviderName} with status code {StatusCode}.",
                    ProviderName,
                    response.StatusCode);

                return new TelephonyDirectoryResult
                {
                    Succeeded = false,
                    Error = S["Asterisk could not load the directory."].Value,
                };
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var entries = new List<TelephonyDirectoryEntry>();

            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var endpoint in document.RootElement.EnumerateArray())
                {
                    var resource = ReadString(endpoint, "resource");

                    if (string.IsNullOrWhiteSpace(resource))
                    {
                        continue;
                    }

                    var technology = ReadString(endpoint, "technology");
                    var state = ReadString(endpoint, "state");
                    var detailParts = new[] { technology, state }
                        .Where(value => !string.IsNullOrWhiteSpace(value));

                    entries.Add(new TelephonyDirectoryEntry
                    {
                        Id = string.IsNullOrWhiteSpace(technology) ? resource : $"{technology}/{resource}",
                        DisplayName = resource,
                        Destination = resource,
                        Extension = resource.All(char.IsDigit) ? resource : null,
                        Detail = string.Join(" - ", detailParts),
                    });
                }
            }

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
            _logger.LogError(ex, "An error occurred while loading the Asterisk directory for provider {ProviderName}.", ProviderName);

            return new TelephonyDirectoryResult
            {
                Succeeded = false,
                Error = S["Asterisk could not load the directory."].Value,
            };
        }
    }

    protected abstract ValueTask<AsteriskResolvedSettings> GetResolvedSettingsAsync(CancellationToken cancellationToken);

    protected static TelephonyCapabilities GetCapabilities(string endpointTemplate, bool hasVoicemailConfiguration)
    {
        var capabilities =
            TelephonyCapabilities.Dial |
            TelephonyCapabilities.Hangup |
            TelephonyCapabilities.SendDigits |
            TelephonyCapabilities.ReceiveCalls |
            TelephonyCapabilities.Hold |
            TelephonyCapabilities.Resume |
            TelephonyCapabilities.Mute |
            TelephonyCapabilities.Merge |
            TelephonyCapabilities.Directory;

        if (AsteriskSettingsUtilities.IsImmediateConnectionEndpoint(endpointTemplate))
        {
            capabilities |= TelephonyCapabilities.Transfer;
        }

        if (hasVoicemailConfiguration)
        {
            capabilities |= TelephonyCapabilities.Voicemail;
        }

        return capabilities;
    }

    private async Task<TelephonyResult> ExecuteCallActionAsync(
        string callId,
        HttpMethod method,
        string pathTemplate,
        IDictionary<string, string> query,
        IDictionary<string, string> stateVariables,
        Func<TelephonyCall> onSuccess,
        string errorMessage,
        string missingCallIdMessage,
        CancellationToken cancellationToken,
        bool succeedWhenChannelIsMissing = false)
    {
        if (string.IsNullOrWhiteSpace(callId))
        {
            return TelephonyResult.Failed(missingCallIdMessage);
        }

        var settings = await GetResolvedSettingsAsync(cancellationToken);

        if (!IsConfigured(settings))
        {
            return NotConfigured();
        }

        try
        {
            var path = pathTemplate.Replace("{callId}", Uri.EscapeDataString(callId), StringComparison.Ordinal);

            using var response = await SendAsync(settings, method, path, query, null, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                if (succeedWhenChannelIsMissing && response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    if (_logger.IsEnabled(LogLevel.Information))
                    {
                        _logger.LogInformation(
                            "Asterisk call action for provider {ProviderName} reached the requested terminal state because channel {CallId} no longer exists.",
                            ProviderName,
                            callId);
                    }

                    return TelephonyResult.Success(onSuccess?.Invoke());
                }

                var responseBody = await ReadResponseBodyAsync(response, cancellationToken);

                _logger.LogError(
                    "Asterisk rejected a call action request for provider {ProviderName}. Path: {Path}. Status code: {StatusCode}. Response: {ResponseBody}",
                    ProviderName,
                    path,
                    response.StatusCode,
                    responseBody);

                return TelephonyResult.Failed(ResolveActionErrorMessage(errorMessage, responseBody));
            }

            await SetChannelVariablesAsync(settings, callId, stateVariables, cancellationToken);

            return TelephonyResult.Success(onSuccess?.Invoke());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while executing an Asterisk call action for provider {ProviderName}.", ProviderName);

            return TelephonyResult.Failed(errorMessage);
        }
    }

    private static bool TryMapLookupState(string state, out CallState mapped)
    {
        mapped = state?.Trim().ToLowerInvariant() switch
        {
            "down" or "dialing" or "reserved" or "offhook" or "pre-ring" => CallState.Connecting,
            "ring" or "ringing" => CallState.Ringing,
            "up" or "connected" => CallState.Connected,
            "hold" or "held" => CallState.OnHold,
            "hungup" or "destroyed" or "disconnected" => CallState.Disconnected,
            "busy" => CallState.Failed,
            _ => (CallState)(-1),
        };

        return Enum.IsDefined(mapped);
    }

    private async Task<bool?> GetChannelBooleanVariableAsync(
        AsteriskResolvedSettings settings,
        string callId,
        string variableName,
        CancellationToken cancellationToken)
    {
        var value = await GetChannelVariableAsync(settings, callId, variableName, cancellationToken);

        if (bool.TryParse(value, out var parsed))
        {
            return parsed;
        }

        return value switch
        {
            "1" => true,
            "0" => false,
            _ => null,
        };
    }

    private async Task<string> GetChannelVariableAsync(
        AsteriskResolvedSettings settings,
        string callId,
        string variableName,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            settings,
            HttpMethod.Get,
            $"channels/{Uri.EscapeDataString(callId)}/variable",
            new Dictionary<string, string>
            {
                ["variable"] = variableName,
            },
            null,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Asterisk could not query channel variable {VariableName} for provider {ProviderName} call {CallId}. Status code: {StatusCode}.",
                variableName,
                ProviderName,
                callId,
                response.StatusCode);

            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        return ReadString(document.RootElement, "value");
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

    private static string ReadNestedString(JsonElement element, string propertyName, string nestedPropertyName)
    {
        return element.TryGetProperty(propertyName, out var value) &&
            value.ValueKind == JsonValueKind.Object &&
            value.TryGetProperty(nestedPropertyName, out var nestedValue) &&
            nestedValue.ValueKind == JsonValueKind.String
            ? nestedValue.GetString()
            : null;
    }

    private HttpClient CreateClient(AsteriskResolvedSettings settings)
    {
        var client = _httpClientFactory.CreateClient(AsteriskConstants.HttpClientName);
        client.BaseAddress = new Uri(settings.BaseUrl, UriKind.Absolute);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.UserName}:{settings.Password}")));

        return client;
    }

    private TelephonyCall BuildCall(
        string callId,
        CallState state,
        bool isOnHold = false,
        bool isMuted = false,
        CallDirection direction = CallDirection.Outbound,
        IDictionary<string, object> metadata = null)
        => new()
        {
            CallId = callId,
            State = state,
            Direction = direction,
            IsOnHold = isOnHold,
            IsMuted = isMuted,
            ProviderName = ProviderName,
            Metadata = metadata is null ? [] : new Dictionary<string, object>(metadata, StringComparer.OrdinalIgnoreCase),
        };

    private async Task SetChannelVariablesAsync(
        AsteriskResolvedSettings settings,
        string callId,
        IDictionary<string, string> variables,
        CancellationToken cancellationToken)
    {
        if (variables is null || variables.Count == 0)
        {
            return;
        }

        foreach (var entry in variables)
        {
            await SetChannelVariableAsync(settings, callId, entry.Key, entry.Value, cancellationToken);
        }
    }

    private async Task SetChannelVariablesAsync(
        AsteriskResolvedSettings settings,
        string callId,
        IDictionary<string, object> metadata,
        CancellationToken cancellationToken)
    {
        if (metadata is null || metadata.Count == 0)
        {
            return;
        }

        foreach (var entry in metadata)
        {
            var value = NormalizeMetadataValue(entry.Value);

            if (string.IsNullOrWhiteSpace(entry.Key) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            await SetChannelVariableAsync(settings, callId, BuildMetadataVariableName(entry.Key), value, cancellationToken);
        }
    }

    private async Task<bool> SetChannelVariableAsync(
        AsteriskResolvedSettings settings,
        string callId,
        string variableName,
        string value,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            settings,
            HttpMethod.Post,
            $"channels/{Uri.EscapeDataString(callId)}/variable",
            new Dictionary<string, string>
            {
                ["variable"] = variableName,
                ["value"] = value,
            },
            null,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await ReadResponseBodyAsync(response, cancellationToken);

            _logger.LogWarning(
                "Asterisk rejected a channel-variable request for provider {ProviderName}. CallId: {CallId}. Variable: {Variable}. Status code: {StatusCode}. Response: {ResponseBody}",
                ProviderName,
                callId,
                variableName,
                response.StatusCode,
                responseBody);

            return false;
        }

        return true;
    }

    private async Task DeleteConferenceBridgeWhenEmptyAsync(
        AsteriskResolvedSettings settings,
        string bridgeId,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            settings,
            HttpMethod.Get,
            $"bridges/{Uri.EscapeDataString(bridgeId)}",
            null,
            null,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Asterisk could not inspect conference bridge {BridgeId} for provider {ProviderName}. Status code: {StatusCode}.",
                bridgeId,
                ProviderName,
                response.StatusCode);

            return;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (document.RootElement.TryGetProperty("channels", out var channels) &&
            channels.ValueKind == JsonValueKind.Array &&
            channels.GetArrayLength() > 0)
        {
            return;
        }

        await DeleteBridgeAsync(settings, bridgeId, cancellationToken);
    }

    private async Task DeleteBridgeAsync(
        AsteriskResolvedSettings settings,
        string bridgeId,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            settings,
            HttpMethod.Delete,
            $"bridges/{Uri.EscapeDataString(bridgeId)}",
            null,
            null,
            cancellationToken);

        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
        {
            _logger.LogWarning(
                "Asterisk could not delete conference bridge {BridgeId} for provider {ProviderName}. Status code: {StatusCode}.",
                bridgeId,
                ProviderName,
                response.StatusCode);
        }
    }

    private static string ResolveVoicemailExtension(string template, CallReference call)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return null;
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["callId"] = call?.CallId,
        };

        if (call?.Metadata is not null)
        {
            foreach (var entry in call.Metadata)
            {
                var value = NormalizeMetadataValue(entry.Value);

                if (!string.IsNullOrWhiteSpace(entry.Key) && !string.IsNullOrWhiteSpace(value))
                {
                    values[entry.Key] = value;
                }
            }
        }

        var resolved = Regex.Replace(template, "\\{(?<key>[^{}]+)\\}", match =>
        {
            var key = match.Groups["key"].Value;

            return values.TryGetValue(key, out var value)
                ? value
                : string.Empty;
        });

        return resolved.Trim();
    }

    private static string BuildMetadataVariableName(string key)
    {
        var builder = new StringBuilder("CRESTAPPS_METADATA_");

        foreach (var character in key)
        {
            builder.Append(char.IsLetterOrDigit(character)
                ? char.ToUpperInvariant(character)
                : '_');
        }

        return builder.ToString();
    }

    private static string NormalizeMetadataValue(object value)
    {
        return value switch
        {
            null => null,
            string text => text,
            JsonElement element => NormalizeJsonElementValue(element),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString(),
        };
    }

    private static string NormalizeJsonElementValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            _ => element.GetRawText(),
        };
    }

    private static Dictionary<string, string> CreateDialQuery(AsteriskResolvedSettings settings, string endpoint)
    {
        var query = new Dictionary<string, string>
        {
            ["endpoint"] = endpoint,
            ["timeout"] = AsteriskSettingsUtilities.ToInvariantString(settings.TimeoutSeconds),
        };

        query["app"] = settings.ApplicationName;

        return query;
    }

    private string ResolveActionErrorMessage(string defaultErrorMessage, string responseBody)
    {
        if (!string.IsNullOrWhiteSpace(responseBody) &&
            responseBody.Contains("Channel not in Stasis application", StringComparison.OrdinalIgnoreCase))
        {
            return S["This action is not available for the current call."].Value;
        }

        return defaultErrorMessage;
    }

    private static bool IsConfigured(AsteriskResolvedSettings settings)
        => settings is not null &&
            settings.IsEnabled &&
            !string.IsNullOrWhiteSpace(settings.BaseUrl) &&
            !string.IsNullOrWhiteSpace(settings.UserName) &&
            !string.IsNullOrWhiteSpace(settings.Password) &&
            !string.IsNullOrWhiteSpace(settings.ApplicationName);

    private TelephonyResult NotConfigured()
        => TelephonyResult.Failed(S["The telephony provider is not configured."].Value);

    private async Task<HttpResponseMessage> SendAsync(
        AsteriskResolvedSettings settings,
        HttpMethod method,
        string relativePath,
        IDictionary<string, string> query,
        IDictionary<string, string> variables,
        CancellationToken cancellationToken)
    {
        var client = CreateClient(settings);
        var url = relativePath;

        if (query is { Count: > 0 })
        {
            url = QueryHelpers.AddQueryString(url, query);
        }

        using var request = new HttpRequestMessage(method, url);

        if (variables is { Count: > 0 })
        {
            request.Content = JsonContent.Create(new { variables });
        }

        return await client.SendAsync(request, cancellationToken);
    }

    private static async Task<string> ReadIdAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        using var document = JsonDocument.Parse(content);

        if (!document.RootElement.TryGetProperty("id", out var idElement))
        {
            return null;
        }

        return idElement.ValueKind switch
        {
            JsonValueKind.String => idElement.GetString(),
            JsonValueKind.Number => idElement.GetRawText(),
            _ => null,
        };
    }

    private static async Task<string> ReadResponseBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(content) ? "<empty>" : content;
    }
}
