using System.Net.Http.Json;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Internal extension calling for Telnyx: connecting two on-platform browser soft phones without routing
/// through the PSTN. Both legs are Telnyx SIP-over-WebSocket registrations, so extension calling reuses the
/// same two-leg originate-and-bridge orchestration the outbound browser-audio path uses.
/// </summary>
public sealed partial class TelnyxTelephonyProvider
{
    /// <summary>
    /// How long the target's browser rings on an internal extension call before an unanswered call is routed
    /// to the target's voicemail.
    /// </summary>
    private const int ExtensionRingTimeoutSeconds = 25;

    /// <inheritdoc/>
    public async Task<TelephonyResult> DialExtensionAsync(ExtensionDialRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.TargetUserId))
        {
            return TelephonyResult.Failed(S["A resolved extension target is required to place an internal call."].Value);
        }

        if (!_options.IsConfigured)
        {
            return NotConfigured();
        }

        // The caller must be signed in to their browser soft phone: an internal call rings the caller's own
        // registered endpoint first, then dials the target endpoint and bridges the two.
        var callerUserId = string.IsNullOrWhiteSpace(request.CallerUserId)
            ? TryGetMetadataValue(request.Metadata, TelephonyConstants.RequestMetadata.SoftPhoneUserId)
            : request.CallerUserId;

        var callerEndpoint = await ResolveUserSipEndpointAsync(callerUserId, cancellationToken);

        if (callerEndpoint is null)
        {
            return TelephonyResult.Failed(S["You must be signed in to the soft phone to place an internal call."].Value);
        }

        var targetEndpoint = await ResolveUserSipEndpointAsync(request.TargetUserId, cancellationToken);

        if (targetEndpoint is null)
        {
            // The target has no live browser registration, so they cannot be reached by extension right now.
            // The telephony layer's no-answer/voicemail handling covers the ringing-but-unanswered case; an
            // unregistered target is reported as unavailable.
            return TelephonyResult.Failed(S["Extension {0} is not available right now.", request.Extension].Value);
        }

        var callerId = string.IsNullOrWhiteSpace(request.From) ? _options.DefaultOutboundCallerId : request.From;

        // Ring the caller's browser first (agent leg), carrying the target endpoint as the destination to dial
        // once they answer. This is exactly the outbound browser-bridge flow, with a SIP target instead of a
        // PSTN number, so the existing bridge orchestrator advances it with no special-casing.
        var bridgeRequest = new DialRequest
        {
            To = targetEndpoint,
            From = callerId,
            Metadata = request.Metadata,
        };

        var result = await DialBrowserBridgeAsync(
            bridgeRequest,
            callerId,
            callerEndpoint,
            cancellationToken,
            voicemailRecipientUserId: request.TargetUserId,
            ringTimeoutSeconds: ExtensionRingTimeoutSeconds);

        // Show the friendly extension/display name in history rather than the raw SIP uri that was dialed.
        if (result.Succeeded && result.Call is not null)
        {
            result.Call.To = string.IsNullOrWhiteSpace(request.TargetDisplayName) ? request.Extension : request.TargetDisplayName;
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<TelephonyResult> AddExtensionToConferenceAsync(ExtensionConferenceRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.TargetUserId))
        {
            return TelephonyResult.Failed(S["A resolved extension target is required to add to the conference."].Value);
        }

        var activeCallId = request.ActiveCall?.CallId;

        if (string.IsNullOrWhiteSpace(activeCallId))
        {
            return TelephonyResult.Failed(S["An active call is required to add an extension to a conference."].Value);
        }

        if (!_options.IsConfigured)
        {
            return NotConfigured();
        }

        var targetEndpoint = await ResolveUserSipEndpointAsync(request.TargetUserId, cancellationToken);

        if (targetEndpoint is null)
        {
            return TelephonyResult.Failed(S["Extension {0} is not available right now.", request.Extension].Value);
        }

        var callerId = _options.DefaultOutboundCallerId;
        var conferenceName = $"conf-{activeCallId}";

        // Originate the new participant leg to the target's browser. When it answers, the bridge orchestrator
        // turns the active call into the conference (if it is not already one) and joins this answered leg.
        var body = new Dictionary<string, object>
        {
            ["connection_id"] = _options.ConnectionId,
            ["to"] = targetEndpoint,
            ["client_state"] = new TelnyxOutboundBridgeState
            {
                Intent = TelnyxOutboundBridgeState.ConferenceExtensionLegIntent,
                PeerCallControlId = activeCallId,
                ConferenceName = conferenceName,
            }.ToClientState(),
        };

        if (!string.IsNullOrWhiteSpace(callerId))
        {
            body["from"] = callerId;
        }

        try
        {
            using var client = CreateClient();
            using var content = JsonContent.Create(body, options: TelnyxJsonSerializerOptions.Default);
            using var response = await client.PostAsync("calls", content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Telnyx rejected a conference-extension leg with status code {StatusCode}. Response: {Response}",
                    response.StatusCode,
                    (await SafeReadContentAsync(response, cancellationToken)).SanitizeLogValue());

                if (TelephonyProviderResponse.IsAmbiguousStatusCode(response.StatusCode))
                {
                    return TelephonyResult.Unknown(S["Telnyx did not confirm whether the extension was added."].Value);
                }

                return TelephonyResult.Failed(S["Telnyx could not add the extension to the conference."].Value);
            }

            var legCallControlId = await ReadDataStringAsync(response, "call_control_id", cancellationToken);

            return TelephonyResult.Success(BuildCall(
                activeCallId,
                CallState.Connected,
                new Dictionary<string, object>
                {
                    ["isConference"] = true,
                    ["conferenceName"] = conferenceName,
                    ["addedLegCallControlId"] = legCallControlId,
                }));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            _logger.LogError(ex, "An error occurred while adding a Telnyx extension to a conference.");

            return TelephonyResult.Unknown(S["Telnyx did not confirm whether the extension was added."].Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while preparing a Telnyx conference-extension leg.");

            return TelephonyResult.Failed(S["Telnyx could not add the extension to the conference."].Value);
        }
    }

    /// <summary>
    /// Resolves an on-platform user to their current live Telnyx browser SIP endpoint, or <see langword="null"/>
    /// when the user has no live registration.
    /// </summary>
    private async Task<string> ResolveUserSipEndpointAsync(string userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var live = await _credentialStore.ListLiveByUserAsync(userId.Trim(), _clock.UtcNow, cancellationToken);
        var credential = live.Count > 0 ? live[0] : null;

        if (credential is null || string.IsNullOrWhiteSpace(credential.SipUsername))
        {
            return null;
        }

        var sipDomain = string.IsNullOrWhiteSpace(_options.SipDomain) ? TelnyxConstants.DefaultSipDomain : _options.SipDomain;

        return $"sip:{credential.SipUsername}@{sipDomain}";
    }

    private static string TryGetMetadataValue(IDictionary<string, string> metadata, string key)
    {
        if (metadata is not null && metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return null;
    }
}
