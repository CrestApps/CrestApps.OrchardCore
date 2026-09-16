using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Default <see cref="ITelnyxVoiceAgentClient"/> implementation over the Telnyx Call Control v2 REST API.
/// </summary>
public sealed class TelnyxVoiceAgentClient : ITelnyxVoiceAgentClient
{
    private readonly TelnyxApiClient _apiClient;
    private readonly TelnyxOptions _options;

    public TelnyxVoiceAgentClient(
        TelnyxApiClient apiClient,
        IOptionsMonitor<TelnyxOptions> options)
    {
        _apiClient = apiClient;
        _options = options.CurrentValue;
    }

    /// <inheritdoc/>
    public async Task<string> OriginateAsync(string to, string from, TelnyxOutboundBridgeState clientState, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(to);
        ArgumentNullException.ThrowIfNull(clientState);

        var request = new TelnyxOriginateRequest
        {
            To = to,
            From = string.IsNullOrWhiteSpace(from) ? _options.DefaultOutboundCallerId : from,
            ConnectionId = _options.ConnectionId,
            // The client owns the transport encoding, so the state is handed over as JSON. Encoding it here as
            // well produces a value that decodes to base64 rather than to JSON, and every correlation then fails.
            ClientState = clientState.ToClientStateJson(),
        };

        // A redelivered dial command must not place a second call to the same person.
        request.AdditionalFields["command_id"] = $"ai-voice-dial-{clientState.ActivityId}";

        // A PSTN destination is terminated through the outbound voice profile so it routes as an outbound call.
        if (!string.IsNullOrWhiteSpace(_options.OutboundVoiceProfileId))
        {
            request.AdditionalFields["outbound_voice_profile_id"] = _options.OutboundVoiceProfileId;
        }

        var result = await _apiClient.OriginateAsync(request, cancellationToken);

        return result.Succeeded ? result.CallControlId : null;
    }
}
