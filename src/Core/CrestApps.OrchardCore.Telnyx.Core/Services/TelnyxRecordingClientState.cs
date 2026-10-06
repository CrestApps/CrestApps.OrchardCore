using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// The correlation state the platform attaches (as Telnyx <c>client_state</c>) when it starts recording a
/// Contact Center interaction. Telnyx echoes the state back on the <c>call.recording.saved</c> webhook, letting
/// the ingest pipeline map a finished recording to the interaction that owns it without a server-side call
/// registry. It is deliberately separate from <see cref="TelnyxOutboundBridgeState"/>: recording is a
/// per-recording concern, so its state never has to encode (or clobber) the bridge intents that drive call-leg
/// routing.
/// </summary>
public sealed class TelnyxRecordingClientState
{
    /// <summary>
    /// Gets or sets the state intent. Always <see cref="TelnyxConstants.Recording.ClientStateIntent"/> for a
    /// recording state.
    /// </summary>
    [JsonPropertyName("i")]
    public string Intent { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the Contact Center interaction the recording belongs to.
    /// </summary>
    [JsonPropertyName("x")]
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this recording is a voicemail (the caller left a message after
    /// being sent to voicemail). When set, the saved-recording handler flags the interaction as a voicemail so it
    /// surfaces in the recipient agent's voicemail inbox, whether the caller was sent to voicemail by the routing
    /// engine or by the agent's manual "send to voicemail" action.
    /// </summary>
    [JsonPropertyName("v")]
    public bool IsVoicemail { get; set; }

    /// <summary>
    /// Gets or sets the user identifier of the agent the voicemail was left for, when known. It lets the
    /// saved-recording handler resolve the recipient for an agent-initiated voicemail, whose interaction may no
    /// longer carry an agent association.
    /// </summary>
    [JsonPropertyName("u")]
    public string RecipientUserId { get; set; }

    /// <summary>
    /// Gets or sets the kind of call recorded when it is not a Contact Center interaction: <see cref="AiCallKind"/>
    /// or <see cref="SoftPhoneCallKind"/>. Empty for an interaction's recording.
    /// </summary>
    [JsonPropertyName("k")]
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets the CRM activity an automated voice agent's call belongs to.
    /// </summary>
    [JsonPropertyName("a")]
    public string ActivityId { get; set; }

    /// <summary>
    /// Gets or sets the user identifier of the agent who dialed a number on the soft phone.
    /// </summary>
    [JsonPropertyName("o")]
    public string AgentUserId { get; set; }

    /// <summary>
    /// Gets or sets the soft phone's identifier of the call, which is the agent's leg.
    /// </summary>
    [JsonPropertyName("c")]
    public string TelephonyCallId { get; set; }

    /// <summary>
    /// Gets or sets the other party's number.
    /// </summary>
    [JsonPropertyName("n")]
    public string CustomerNumber { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the other party placed the call.
    /// </summary>
    [JsonPropertyName("in")]
    public bool? IsInbound { get; set; }

    /// <summary>
    /// The <see cref="Kind"/> of an automated voice agent's call.
    /// </summary>
    public const string AiCallKind = "ai";

    /// <summary>
    /// The <see cref="Kind"/> of a number an agent dialed on the soft phone's keypad.
    /// </summary>
    public const string SoftPhoneCallKind = "sp";

    /// <summary>
    /// Gets a value indicating whether the recording belongs to an automated voice agent's call.
    /// </summary>
    [JsonIgnore]
    public bool IsAiCall => Kind == AiCallKind;

    /// <summary>
    /// Gets a value indicating whether the recording belongs to a number dialed on the soft phone.
    /// </summary>
    [JsonIgnore]
    public bool IsSoftPhoneCall => Kind == SoftPhoneCallKind;

    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Creates a recording client state for the supplied interaction.
    /// </summary>
    /// <param name="interactionId">The interaction the recording belongs to.</param>
    public static TelnyxRecordingClientState ForInteraction(string interactionId)
        => new()
        {
            Intent = TelnyxConstants.Recording.ClientStateIntent,
            InteractionId = interactionId,
        };

    /// <summary>
    /// Creates a recording client state for an automated voice agent's call.
    /// </summary>
    /// <param name="activityId">The CRM activity the call belongs to.</param>
    /// <param name="customerNumber">The other party's number.</param>
    /// <param name="isInbound">Whether the other party placed the call.</param>
    public static TelnyxRecordingClientState ForAiCall(string activityId, string customerNumber, bool isInbound)
        => new()
        {
            Intent = TelnyxConstants.Recording.ClientStateIntent,
            Kind = AiCallKind,
            ActivityId = activityId,
            CustomerNumber = customerNumber,
            IsInbound = isInbound,
        };

    /// <summary>
    /// Creates a recording client state for a number an agent dialed on the soft phone's keypad.
    /// </summary>
    /// <param name="agentUserId">The user identifier of the agent who dialed.</param>
    /// <param name="telephonyCallId">The soft phone's identifier of the call (the agent's leg).</param>
    /// <param name="customerNumber">The number dialed.</param>
    public static TelnyxRecordingClientState ForSoftPhoneCall(string agentUserId, string telephonyCallId, string customerNumber)
        => new()
        {
            Intent = TelnyxConstants.Recording.ClientStateIntent,
            Kind = SoftPhoneCallKind,
            AgentUserId = agentUserId,
            TelephonyCallId = telephonyCallId,
            CustomerNumber = customerNumber,
            IsInbound = false,
        };

    /// <summary>
    /// Creates a recording client state for a voicemail left on the supplied interaction.
    /// </summary>
    /// <param name="interactionId">The interaction the voicemail belongs to.</param>
    /// <param name="recipientUserId">The user identifier of the agent the voicemail was left for, when known.</param>
    public static TelnyxRecordingClientState ForVoicemail(string interactionId, string recipientUserId)
        => new()
        {
            Intent = TelnyxConstants.Recording.ClientStateIntent,
            InteractionId = interactionId,
            IsVoicemail = true,
            RecipientUserId = recipientUserId,
        };

    /// <summary>
    /// Creates the client state attached to the voicemail greeting (the <c>speak</c>/<c>playback_start</c> that
    /// plays "leave your message"). Telnyx echoes it on the greeting's ended webhook, which is the signal to begin
    /// the beep-and-record, so the greeting itself is never captured inside the caller's message.
    /// </summary>
    /// <param name="interactionId">The interaction the voicemail belongs to.</param>
    /// <param name="recipientUserId">The user identifier of the agent the voicemail was left for, when known.</param>
    public static TelnyxRecordingClientState ForVoicemailGreeting(string interactionId, string recipientUserId)
        => new()
        {
            Intent = TelnyxConstants.Recording.VoicemailGreetingClientStateIntent,
            InteractionId = interactionId,
            IsVoicemail = true,
            RecipientUserId = recipientUserId,
        };

    /// <summary>
    /// Serializes the state to the base64 form Telnyx expects for a <c>client_state</c> value.
    /// </summary>
    public string ToClientState()
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(this, _options)));

    /// <summary>
    /// Attempts to parse a decoded client-state string into a recording <see cref="TelnyxRecordingClientState"/>
    /// (the state set when recording started).
    /// </summary>
    /// <param name="decodedClientState">The already base64-decoded client-state JSON.</param>
    /// <param name="state">The parsed state when successful.</param>
    /// <returns><see langword="true"/> when the value is a recording client state carrying an interaction id, or naming the automated voice agent's call or soft phone call it recorded.</returns>
    public static bool TryParse(string decodedClientState, out TelnyxRecordingClientState state)
        => TryParse(decodedClientState, TelnyxConstants.Recording.ClientStateIntent, out state);

    /// <summary>
    /// Attempts to parse a decoded client-state string into a voicemail-greeting client state (the state echoed on
    /// the greeting's ended webhook that signals it is time to start recording).
    /// </summary>
    /// <param name="decodedClientState">The already base64-decoded client-state JSON.</param>
    /// <param name="state">The parsed state when successful.</param>
    /// <returns><see langword="true"/> when the value is a voicemail-greeting client state carrying an interaction id.</returns>
    public static bool TryParseGreeting(string decodedClientState, out TelnyxRecordingClientState state)
        => TryParse(decodedClientState, TelnyxConstants.Recording.VoicemailGreetingClientStateIntent, out state);

    private static bool TryParse(string decodedClientState, string expectedIntent, out TelnyxRecordingClientState state)
    {
        state = null;

        if (string.IsNullOrWhiteSpace(decodedClientState))
        {
            return false;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<TelnyxRecordingClientState>(decodedClientState, _options);

            // A recording that is not an interaction's is still traced back to its call by what it names instead.
            var namesItsCall = !string.IsNullOrWhiteSpace(parsed?.InteractionId) ||
                (parsed is not null && expectedIntent == TelnyxConstants.Recording.ClientStateIntent &&
                    (parsed.IsAiCall && !string.IsNullOrWhiteSpace(parsed.ActivityId) ||
                    parsed.IsSoftPhoneCall && !string.IsNullOrWhiteSpace(parsed.AgentUserId)));

            if (parsed is null ||
                parsed.Intent != expectedIntent ||
                !namesItsCall)
            {
                return false;
            }

            state = parsed;

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
