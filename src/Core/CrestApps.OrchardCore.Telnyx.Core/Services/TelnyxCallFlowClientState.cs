using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// The <c>client_state</c> the platform attaches to a Telnyx command whose webhooks it has to act on later: the new leg
/// of an external transfer, whose answer or hang-up says whether the transfer worked, and a last message the caller is
/// told before the call is ended, whose <c>call.speak.ended</c> is the moment to hang up.
/// </summary>
/// <remarks>
/// Telnyx echoes a command's <c>client_state</c> on every later webhook for the leg, and <c>target_leg_client_state</c>
/// on every webhook for the leg a transfer creates (https://developers.telnyx.com/api-reference/call-commands/transfer-call).
/// Its intents are distinct from the outbound-bridge and recording intents, so neither of those ever mistakes it for
/// one of theirs.
/// </remarks>
public sealed class TelnyxCallFlowClientState
{
    /// <summary>
    /// The intent carried by the leg an external transfer rings.
    /// </summary>
    public const string TransferLegIntent = "cc-xfer";

    /// <summary>
    /// The intent carried by a last message after which the call is ended.
    /// </summary>
    public const string HangUpAfterSpeechIntent = "cc-bye";

    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Gets or sets the intent.
    /// </summary>
    [JsonPropertyName("i")]
    public string Intent { get; set; }

    /// <summary>
    /// Gets or sets the Contact Center interaction the command was issued for.
    /// </summary>
    [JsonPropertyName("x")]
    public string InteractionId { get; set; }

    /// <summary>
    /// The state for the leg an external transfer rings.
    /// </summary>
    /// <param name="interactionId">The caller's interaction.</param>
    public static TelnyxCallFlowClientState ForTransferLeg(string interactionId)
        => new() { Intent = TransferLegIntent, InteractionId = interactionId };

    /// <summary>
    /// The state for a last message after which the call is ended.
    /// </summary>
    public static TelnyxCallFlowClientState ForHangUpAfterSpeech()
        => new() { Intent = HangUpAfterSpeechIntent };

    /// <summary>
    /// Serializes the state without encoding it, for an API method that encodes <c>client_state</c> itself.
    /// </summary>
    public string ToJson()
        => JsonSerializer.Serialize(this, _options);

    /// <summary>
    /// Serializes the state to the base64 form Telnyx expects, for a command body built by hand.
    /// </summary>
    public string ToClientState()
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(ToJson()));

    /// <summary>
    /// Reads a decoded <c>client_state</c> as one of these states.
    /// </summary>
    /// <param name="decodedClientState">The client state as the webhook parser decoded it.</param>
    /// <param name="state">The state, when it is one.</param>
    public static bool TryParse(string decodedClientState, out TelnyxCallFlowClientState state)
    {
        state = null;

        if (string.IsNullOrWhiteSpace(decodedClientState) || !decodedClientState.TrimStart().StartsWith('{'))
        {
            return false;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<TelnyxCallFlowClientState>(decodedClientState, _options);

            if (parsed?.Intent is not (TransferLegIntent or HangUpAfterSpeechIntent))
            {
                return false;
            }

            if (parsed.Intent == TransferLegIntent && string.IsNullOrWhiteSpace(parsed.InteractionId))
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
