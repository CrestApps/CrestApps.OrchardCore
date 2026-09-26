using CrestApps.OrchardCore.Telnyx.Models;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Default <see cref="ITelnyxVoiceAgentClient"/> implementation over the Telnyx Call Control v2 REST API.
/// </summary>
public sealed class TelnyxVoiceAgentClient : ITelnyxVoiceAgentClient
{
    /// <summary>
    /// How long an automated call rings before it is given up on.
    /// </summary>
    /// <remarks>
    /// Longer than the provider's 30-second default, because a customer's voicemail is often set to pick up at about
    /// that point: live, a call was abandoned just before the voicemail answered, so no message was left and the
    /// attempt counted as nobody there. A voicemail that answers is a message left; a call cut off first is nothing.
    /// </remarks>
    public const int RingTimeoutSeconds = 45;

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
            TimeoutSeconds = RingTimeoutSeconds,
        };

        // A redelivered dial command must not place a second call to the same person.
        request.AdditionalFields["command_id"] = $"ai-voice-dial-{clientState.ActivityId}";

        // Whether a person or a machine answered, and when a machine's greeting ends, told by the provider rather
        // than guessed from a transcript: a short greeting plays out under the opening line before anything of it
        // is transcribed. The answer arrives as its own events a few seconds after the call is answered.
        var detection = AnsweringMachineDetectionMode(_options.AnsweringMachineDetection);

        if (detection is not null)
        {
            request.AdditionalFields["answering_machine_detection"] = detection;
        }

        // A PSTN destination is terminated through the outbound voice profile so it routes as an outbound call.
        if (!string.IsNullOrWhiteSpace(_options.OutboundVoiceProfileId))
        {
            request.AdditionalFields["outbound_voice_profile_id"] = _options.OutboundVoiceProfileId;
        }

        var result = await _apiClient.OriginateAsync(request, cancellationToken);

        return result.Succeeded ? result.CallControlId : null;
    }

    /// <summary>
    /// The provider's name for a detection setting, or <see langword="null"/> when detection is off.
    /// </summary>
    /// <remarks>
    /// Standard detection is asked to find the end of the greeting as well, on silence or a tone, because the
    /// moment to leave the message is the point of asking at all.
    /// </remarks>
    /// <param name="detection">The configured detection.</param>
    public static string AnsweringMachineDetectionMode(TelnyxAnsweringMachineDetection detection)
        => detection switch
        {
            TelnyxAnsweringMachineDetection.Premium => "premium",
            TelnyxAnsweringMachineDetection.Standard => "greeting_end",
            _ => null,
        };
}
