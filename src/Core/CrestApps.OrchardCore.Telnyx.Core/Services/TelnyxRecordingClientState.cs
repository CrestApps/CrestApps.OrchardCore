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
    /// Serializes the state to the base64 form Telnyx expects for a <c>client_state</c> value.
    /// </summary>
    public string ToClientState()
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(this, _options)));

    /// <summary>
    /// Attempts to parse a decoded client-state string into a <see cref="TelnyxRecordingClientState"/>.
    /// </summary>
    /// <param name="decodedClientState">The already base64-decoded client-state JSON.</param>
    /// <param name="state">The parsed state when successful.</param>
    /// <returns><see langword="true"/> when the value is a recording client state carrying an interaction id.</returns>
    public static bool TryParse(string decodedClientState, out TelnyxRecordingClientState state)
    {
        state = null;

        if (string.IsNullOrWhiteSpace(decodedClientState))
        {
            return false;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<TelnyxRecordingClientState>(decodedClientState, _options);

            if (parsed is null ||
                parsed.Intent != TelnyxConstants.Recording.ClientStateIntent ||
                string.IsNullOrWhiteSpace(parsed.InteractionId))
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
