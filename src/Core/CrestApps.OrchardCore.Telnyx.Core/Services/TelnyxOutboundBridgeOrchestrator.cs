using System.Net.Http.Headers;
using System.Net.Http.Json;
using CrestApps.Core.Support;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Default implementation of <see cref="ITelnyxOutboundBridgeOrchestrator"/>. It reacts to the
/// <c>call.answered</c> events of the two legs the platform created for a browser-audio outbound call and
/// issues the follow-up Telnyx Call Control commands over REST. All correlation travels in the leg's
/// <c>client_state</c>, so no server-side call registry is required.
/// </summary>
public sealed class TelnyxOutboundBridgeOrchestrator : ITelnyxOutboundBridgeOrchestrator
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TelnyxOutboundBridgeOrchestrator> _logger;
    private readonly TelnyxOptions _options;

    public TelnyxOutboundBridgeOrchestrator(
        IHttpClientFactory httpClientFactory,
        ILogger<TelnyxOutboundBridgeOrchestrator> logger,
        IOptionsMonitor<TelnyxOptions> telnyxOptions)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _options = telnyxOptions.CurrentValue;
    }

    /// <inheritdoc/>
    public async Task<TelnyxOutboundBridgeLeg> AdvanceAsync(TelnyxCallEvent callEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callEvent);

        if (!TelnyxOutboundBridgeState.TryParse(callEvent.ClientState, out var state))
        {
            return TelnyxOutboundBridgeLeg.None;
        }

        var isAnswered = string.Equals(callEvent.EventType?.Trim(), "call.answered", StringComparison.OrdinalIgnoreCase);

        if (state.Intent == TelnyxOutboundBridgeState.AgentLegIntent)
        {
            // The agent's browser answered the leg we rang; dial the destination it wanted to reach.
            if (isAnswered && _options.IsConfigured && !string.IsNullOrWhiteSpace(state.Destination))
            {
                await DialDestinationAsync(agentLegCallControlId: callEvent.CallControlId, state, cancellationToken);
            }

            return TelnyxOutboundBridgeLeg.AgentLeg;
        }

        if (state.Intent == TelnyxOutboundBridgeState.ContactCenterAgentLegIntent)
        {
            // The Contact Center agent's browser answered; bridge it to the already-answered caller leg. The
            // agent leg is a tracked leg of the interaction, so let its events flow to normalization (return
            // None) rather than treating it as an internal leg to hide.
            if (isAnswered && _options.IsConfigured && !string.IsNullOrWhiteSpace(state.PeerCallControlId))
            {
                await BridgeAsync(destinationLegCallControlId: callEvent.CallControlId, agentLegCallControlId: state.PeerCallControlId, cancellationToken);
            }

            return TelnyxOutboundBridgeLeg.None;
        }

        if (state.Intent == TelnyxOutboundBridgeState.ConferenceExtensionLegIntent)
        {
            // An internal extension participant answered; join it to the conference formed from the active call.
            // It is a tracked participant leg, so let its events flow to normalization (return None).
            if (isAnswered && _options.IsConfigured && !string.IsNullOrWhiteSpace(state.PeerCallControlId))
            {
                await JoinConferenceAsync(answeredLegCallControlId: callEvent.CallControlId, state, cancellationToken);
            }

            return TelnyxOutboundBridgeLeg.None;
        }

        // The destination answered; bridge it to the agent leg that has been waiting.
        if (isAnswered && _options.IsConfigured && !string.IsNullOrWhiteSpace(state.PeerCallControlId))
        {
            await BridgeAsync(destinationLegCallControlId: callEvent.CallControlId, agentLegCallControlId: state.PeerCallControlId, cancellationToken);

            return TelnyxOutboundBridgeLeg.DestinationLeg;
        }

        // The destination hung up without answering. For an internal extension call that names a voicemail
        // recipient, route the still-connected caller (agent) leg to that user's voicemail instead of just
        // ending the call. A caller-canceled or normal-clearing hangup is not a no-answer, so it is left alone.
        if (IsNoAnswerHangup(callEvent) &&
            _options.IsConfigured &&
            !string.IsNullOrWhiteSpace(state.VoicemailRecipientUserId) &&
            !string.IsNullOrWhiteSpace(state.PeerCallControlId))
        {
            await RouteToVoicemailAsync(
                agentLegCallControlId: state.PeerCallControlId,
                recipientUserId: state.VoicemailRecipientUserId,
                cancellationToken);
        }

        return TelnyxOutboundBridgeLeg.DestinationLeg;
    }

    // Telnyx hangup causes that mean the target never answered (as opposed to a normal end after a conversation
    // or the caller canceling before answer). Only these route the caller to voicemail.
    private static readonly HashSet<string> _noAnswerHangupCauses = new(StringComparer.OrdinalIgnoreCase)
    {
        "TIMEOUT",
        "NO_ANSWER",
        "USER_BUSY",
        "CALL_REJECTED",
        "NORMAL_TEMPORARY_FAILURE",
    };

    private static bool IsNoAnswerHangup(TelnyxCallEvent callEvent)
    {
        if (!string.Equals(callEvent.EventType?.Trim(), "call.hangup", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(callEvent.HangupCause) &&
            _noAnswerHangupCauses.Contains(callEvent.HangupCause.Trim());
    }

    private async Task RouteToVoicemailAsync(string agentLegCallControlId, string recipientUserId, CancellationToken cancellationToken)
    {
        // The caller's leg is still up (the destination never answered, so it was never bridged). Start a
        // beep-and-record on that leg tagged as the recipient's voicemail, so the existing saved-recording
        // pipeline ingests it into that user's voicemail inbox. The greeting is intentionally not spoken here so
        // this path stays independent of the voicemail-greeting media model; a leading beep tells the caller to
        // record.
        var body = new Dictionary<string, object>
        {
            ["client_state"] = TelnyxRecordingClientState.ForVoicemail(agentLegCallControlId, recipientUserId).ToClientState(),
            ["format"] = TelnyxConstants.Recording.Format,
            ["channels"] = "single",
            ["play_beep"] = true,
            ["command_id"] = $"ext-vm-{agentLegCallControlId}",
        };

        try
        {
            using var client = CreateClient();
            using var content = JsonContent.Create(body, options: TelnyxJsonSerializerOptions.Default);
            using var response = await client.PostAsync(
                $"calls/{Uri.EscapeDataString(agentLegCallControlId)}/actions/record_start",
                content,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Telnyx rejected starting extension voicemail recording with status code {StatusCode}. Response: {Response}",
                    response.StatusCode,
                    (await SafeReadContentAsync(response, cancellationToken)).SanitizeLogValue());
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while routing an unanswered extension call to voicemail.");
        }
    }

    private async Task JoinConferenceAsync(string answeredLegCallControlId, TelnyxOutboundBridgeState state, CancellationToken cancellationToken)
    {
        var conferenceName = string.IsNullOrWhiteSpace(state.ConferenceName)
            ? $"conf-{state.PeerCallControlId}"
            : state.ConferenceName;

        try
        {
            using var client = CreateClient();

            // Ensure the conference exists, formed from the active call. The first extension add creates it; a
            // later add finds the existing one. command_id makes a redelivered create idempotent.
            var conferenceId = await EnsureConferenceAsync(client, conferenceName, state.PeerCallControlId, cancellationToken);

            if (string.IsNullOrWhiteSpace(conferenceId))
            {
                _logger.LogError("Could not resolve the Telnyx conference '{ConferenceName}' to add an extension participant.", conferenceName.SanitizeLogValue());

                return;
            }

            using var joinContent = JsonContent.Create(
                new Dictionary<string, object>
                {
                    ["call_control_id"] = answeredLegCallControlId,
                    ["command_id"] = $"ext-conf-join-{answeredLegCallControlId}",
                },
                options: TelnyxJsonSerializerOptions.Default);
            using var joinResponse = await client.PostAsync(
                $"conferences/{Uri.EscapeDataString(conferenceId)}/actions/join",
                joinContent,
                cancellationToken);

            if (!joinResponse.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Telnyx rejected joining an extension participant to conference '{ConferenceName}' with status code {StatusCode}. Response: {Response}",
                    conferenceName.SanitizeLogValue(),
                    joinResponse.StatusCode,
                    (await SafeReadContentAsync(joinResponse, cancellationToken)).SanitizeLogValue());
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while joining an extension participant to a Telnyx conference.");
        }
    }

    private static async Task<string> EnsureConferenceAsync(HttpClient client, string conferenceName, string activeCallControlId, CancellationToken cancellationToken)
    {
        using var createContent = JsonContent.Create(
            new Dictionary<string, object>
            {
                ["name"] = conferenceName,
                ["call_control_id"] = activeCallControlId,
                ["command_id"] = $"ext-conf-create-{activeCallControlId}",
            },
            options: TelnyxJsonSerializerOptions.Default);
        using var createResponse = await client.PostAsync("conferences", createContent, cancellationToken);

        if (createResponse.IsSuccessStatusCode)
        {
            return await ReadConferenceIdAsync(createResponse, cancellationToken);
        }

        // The conference already exists (a prior extension add created it); look it up by its deterministic name.
        using var listResponse = await client.GetAsync(
            $"conferences?filter[name]={Uri.EscapeDataString(conferenceName)}",
            cancellationToken);

        if (!listResponse.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await listResponse.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await System.Text.Json.JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (document.RootElement.TryGetProperty("data", out var data) &&
            data.ValueKind == System.Text.Json.JsonValueKind.Array &&
            data.GetArrayLength() > 0 &&
            data[0].TryGetProperty("id", out var idElement))
        {
            return idElement.GetString();
        }

        return null;
    }

    private static async Task<string> ReadConferenceIdAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await System.Text.Json.JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (document.RootElement.TryGetProperty("data", out var data) &&
            data.ValueKind == System.Text.Json.JsonValueKind.Object &&
            data.TryGetProperty("id", out var idElement))
        {
            return idElement.GetString();
        }

        return null;
    }

    private async Task DialDestinationAsync(string agentLegCallControlId, TelnyxOutboundBridgeState agentState, CancellationToken cancellationToken)
    {
        var body = new Dictionary<string, object>
        {
            ["connection_id"] = _options.ConnectionId,
            ["to"] = agentState.Destination,
            // Correlate the destination leg back to the agent leg so its call.answered can bridge the two, and
            // carry the voicemail recipient so a no-answer destination hangup can send the caller to voicemail.
            ["client_state"] = new TelnyxOutboundBridgeState
            {
                Intent = TelnyxOutboundBridgeState.DestinationLegIntent,
                PeerCallControlId = agentLegCallControlId,
                VoicemailRecipientUserId = agentState.VoicemailRecipientUserId,
            }.ToClientState(),
            // Telnyx de-duplicates by command_id, so a redelivered agent-answered webhook cannot place a
            // second destination call.
            ["command_id"] = $"ob-dest-{agentLegCallControlId}",
        };

        if (!string.IsNullOrWhiteSpace(agentState.CallerId))
        {
            body["from"] = agentState.CallerId;
        }

        if (!string.IsNullOrWhiteSpace(_options.OutboundVoiceProfileId))
        {
            body["outbound_voice_profile_id"] = _options.OutboundVoiceProfileId;
        }

        // Bound the ring so an unanswered internal extension call is released and can fall to voicemail rather
        // than ringing indefinitely.
        if (agentState.RingTimeoutSeconds is > 0)
        {
            body["timeout_secs"] = agentState.RingTimeoutSeconds.Value;
        }

        try
        {
            using var client = CreateClient();
            using var content = JsonContent.Create(body, options: TelnyxJsonSerializerOptions.Default);
            using var response = await client.PostAsync("calls", content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Telnyx rejected the destination leg of an outbound bridge with status code {StatusCode}. Response: {Response}",
                    response.StatusCode,
                    (await SafeReadContentAsync(response, cancellationToken)).SanitizeLogValue());
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while dialing the destination leg of a Telnyx outbound bridge.");
        }
    }

    private async Task BridgeAsync(string destinationLegCallControlId, string agentLegCallControlId, CancellationToken cancellationToken)
    {
        var body = new Dictionary<string, object>
        {
            ["call_control_id"] = agentLegCallControlId,
            ["command_id"] = $"ob-bridge-{destinationLegCallControlId}",
        };

        try
        {
            using var client = CreateClient();
            using var content = JsonContent.Create(body, options: TelnyxJsonSerializerOptions.Default);
            using var response = await client.PostAsync(
                $"calls/{Uri.EscapeDataString(destinationLegCallControlId)}/actions/bridge",
                content,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Telnyx rejected the bridge of an outbound soft-phone call with status code {StatusCode}. Response: {Response}",
                    response.StatusCode,
                    (await SafeReadContentAsync(response, cancellationToken)).SanitizeLogValue());
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while bridging an outbound Telnyx soft-phone call.");
        }
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient(TelnyxConstants.ProviderTechnicalName);
        client.BaseAddress = new Uri(_options.ApiBaseUrl);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        return client;
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
}
