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

        // The destination answered; bridge it to the agent leg that has been waiting.
        if (isAnswered && _options.IsConfigured && !string.IsNullOrWhiteSpace(state.PeerCallControlId))
        {
            await BridgeAsync(destinationLegCallControlId: callEvent.CallControlId, agentLegCallControlId: state.PeerCallControlId, cancellationToken);
        }

        return TelnyxOutboundBridgeLeg.DestinationLeg;
    }

    private async Task DialDestinationAsync(string agentLegCallControlId, TelnyxOutboundBridgeState agentState, CancellationToken cancellationToken)
    {
        var body = new Dictionary<string, object>
        {
            ["connection_id"] = _options.ConnectionId,
            ["to"] = agentState.Destination,
            // Correlate the destination leg back to the agent leg so its call.answered can bridge the two.
            ["client_state"] = new TelnyxOutboundBridgeState
            {
                Intent = TelnyxOutboundBridgeState.DestinationLegIntent,
                PeerCallControlId = agentLegCallControlId,
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
