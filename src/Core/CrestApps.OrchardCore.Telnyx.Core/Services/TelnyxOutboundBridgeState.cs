using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// The correlation state the platform attaches (as Telnyx <c>client_state</c>) to the two legs it creates
/// when bridging an outbound soft-phone call to the caller's browser: the agent leg it rings first, and the
/// destination leg it dials once the agent's browser answers. Telnyx echoes the state back on every event for
/// the leg, letting the webhook orchestration advance the bridge without any server-side call registry.
/// </summary>
public sealed class TelnyxOutboundBridgeState
{
    /// <summary>The intent marking a leg dialed to the agent's browser endpoint.</summary>
    public const string AgentLegIntent = "ob-agent";

    /// <summary>The intent marking the destination leg dialed after the agent's browser answered.</summary>
    public const string DestinationLegIntent = "ob-dest";

    /// <summary>
    /// Gets or sets the leg intent, one of <see cref="AgentLegIntent"/> or <see cref="DestinationLegIntent"/>.
    /// </summary>
    [JsonPropertyName("i")]
    public string Intent { get; set; }

    /// <summary>
    /// Gets or sets the destination address to dial once the agent leg answers (agent-leg state only).
    /// </summary>
    [JsonPropertyName("d")]
    public string Destination { get; set; }

    /// <summary>
    /// Gets or sets the caller id to present to the destination (agent-leg state only).
    /// </summary>
    [JsonPropertyName("f")]
    public string CallerId { get; set; }

    /// <summary>
    /// Gets or sets the agent leg's call control id to bridge to once the destination answers
    /// (destination-leg state only).
    /// </summary>
    [JsonPropertyName("p")]
    public string PeerCallControlId { get; set; }

    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Serializes the state to the base64 form Telnyx expects for a <c>client_state</c> value.
    /// </summary>
    public string ToClientState()
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(this, _options)));

    /// <summary>
    /// Attempts to parse a decoded client-state string into a <see cref="TelnyxOutboundBridgeState"/>.
    /// </summary>
    /// <param name="decodedClientState">The already base64-decoded client-state JSON.</param>
    /// <param name="state">The parsed state when successful.</param>
    /// <returns><see langword="true"/> when the value is one of this feature's outbound-bridge states.</returns>
    public static bool TryParse(string decodedClientState, out TelnyxOutboundBridgeState state)
    {
        state = null;

        if (string.IsNullOrWhiteSpace(decodedClientState))
        {
            return false;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<TelnyxOutboundBridgeState>(decodedClientState, _options);

            if (parsed is null ||
                (parsed.Intent != AgentLegIntent && parsed.Intent != DestinationLegIntent))
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
