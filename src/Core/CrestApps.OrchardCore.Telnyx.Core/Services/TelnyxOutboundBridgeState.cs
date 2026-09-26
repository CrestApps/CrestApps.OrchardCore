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
    /// The intent marking a Contact Center agent leg dialed to the agent's browser to receive an inbound (or
    /// dialer) call. When this leg is answered by the browser, it is bridged to the already-answered caller leg
    /// carried in <see cref="PeerCallControlId"/>.
    /// </summary>
    public const string ContactCenterAgentLegIntent = "cc-agent";

    /// <summary>
    /// The intent marking a Contact Center agent leg rung to the agent's browser while the offer is still ringing,
    /// before the agent has accepted. The browser holds it without ringing it separately and answers it when the
    /// agent accepts; the Contact Center joins it to the caller leg in <see cref="PeerCallControlId"/> only once the
    /// offer in <see cref="ReservationId"/> is accepted and the leg answered, and hangs it up otherwise.
    /// </summary>
    public const string ContactCenterPreDialedAgentLegIntent = "cc-predial";

    /// <summary>
    /// The intent marking a leg dialed to an internal extension's browser endpoint to add it into an active
    /// call as a conference participant. When this leg is answered, the active call carried in
    /// <see cref="PeerCallControlId"/> is turned into (or reused as) the conference named
    /// <see cref="ConferenceName"/>, and this answered leg joins it.
    /// </summary>
    public const string ConferenceExtensionLegIntent = "ext-conf";

    /// <summary>
    /// The intent marking an outbound leg the platform dials to a customer to be handled by an automated AI
    /// voice agent. The leg's own call-control events (answered, transcription, speak-ended, hangup) drive the
    /// AI conversation loop; <see cref="ActivityId"/> ties the leg back to the omnichannel activity it fulfills.
    /// The leg is never bridged to a human agent, so its events are kept out of Contact Center normalization.
    /// </summary>
    public const string AiVoiceLegIntent = "ai-voice";

    /// <summary>
    /// The intent marking the leg a warm transfer rings to the destination the agent is consulting: another agent's
    /// browser or an external number. When it answers it joins the conference named <see cref="ConferenceName"/>,
    /// where the customer in <see cref="PeerCallControlId"/> is held; its answer and hangup are reported to the
    /// Contact Center against the consult in <see cref="ConsultId"/>. It is never bridged to the customer directly
    /// until the agent completes the transfer.
    /// </summary>
    public const string ContactCenterConsultLegIntent = "cc-consult";

    /// <summary>
    /// The intent marking a leg the platform rings to hand a soft-phone call to somebody else: a colleague's
    /// registered browser (which rings it with Answer and Decline, and follows it as its own call once answered), or an
    /// outside number. For a blind transfer <see cref="PeerCallControlId"/> is the party being handed over, who stays
    /// with the transferring agent until this leg answers; for a consult it is the agent's consult leg, joined with
    /// this one in the conference <see cref="ConferenceName"/>. <see cref="TransferOfCallControlId"/> names the
    /// transferring agent's leg either way.
    /// </summary>
    public const string TransferLegIntent = "ob-xfer";

    /// <summary>
    /// The intent marking the leg a supervisor's own soft phone is rung on to listen to, coach or join a Contact Center
    /// call. When it answers it joins the conference <see cref="ConferenceName"/> the customer in
    /// <see cref="PeerCallControlId"/> and the agent in <see cref="PartyCallControlId"/> are moved into, with the Telnyx
    /// supervisor role in <see cref="SupervisorRole"/>. It is never bridged to anyone, and its events are never
    /// normalized as a call of its own. <see cref="RingUserId"/> names the supervisor, and <see cref="MonitorToken"/>
    /// is what the supervisor's phone matches it to the engagement it asked for.
    /// </summary>
    public const string ContactCenterSupervisorLegIntent = "cc-sv";

    /// <summary>
    /// Gets or sets, on a supervisor leg, the Telnyx conference supervisor role it joins with: <c>monitor</c>,
    /// <c>whisper</c> or <c>barge</c>.
    /// </summary>
    [JsonPropertyName("g")]
    public string SupervisorRole { get; set; }

    /// <summary>
    /// Gets or sets, on a supervisor leg, the one-off token the supervisor's phone was told to expect, so it answers this
    /// leg by itself and no other.
    /// </summary>
    [JsonPropertyName("l")]
    public string MonitorToken { get; set; }

    /// <summary>
    /// Gets or sets, on a transfer leg (and on the legs it hands a call over to), the transferring agent's leg the
    /// transfer was made from.
    /// </summary>
    [JsonPropertyName("h")]
    public string TransferOfCallControlId { get; set; }

    /// <summary>
    /// Gets or sets, on an agent leg rung for a warm transfer's consult, the agent's leg of the call being consulted
    /// about -- the call that will be handed over.
    /// </summary>
    [JsonPropertyName("o")]
    public string ConsultOfCallControlId { get; set; }

    /// <summary>
    /// Gets or sets, on a consult's agent leg, the party the consult is about: the other end of
    /// <see cref="ConsultOfCallControlId"/>, handed to the consulted destination when the transfer completes.
    /// </summary>
    [JsonPropertyName("y")]
    public string PartyCallControlId { get; set; }

    /// <summary>
    /// Gets or sets the user a transfer or consult rings, when it rings a colleague rather than a number.
    /// </summary>
    [JsonPropertyName("u")]
    public string TargetUserId { get; set; }

    /// <summary>
    /// Gets or sets the number of the party being transferred, shown to the colleague the call is handed to.
    /// </summary>
    [JsonPropertyName("m")]
    public string PartyNumber { get; set; }

    /// <summary>
    /// Gets or sets, on the agent's leg of a call being transferred and on its party's leg, the leg the transfer rang
    /// (a transfer leg, or a consult's agent leg) while the transfer is under way. The agent hanging up does not release
    /// the party while it is set, and the party hanging up releases that leg too.
    /// </summary>
    [JsonPropertyName("q")]
    public string PendingTransferCallControlId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the destination of a consult answered, on the consult's agent leg: only
    /// then can the call be handed to them.
    /// </summary>
    [JsonPropertyName("s")]
    public bool? TargetAnswered { get; set; }

    /// <summary>
    /// Gets or sets, on the agent's leg of a number dialed from the soft phone or of an extension call, whether the party
    /// in <see cref="PeerCallControlId"/> has answered: <see langword="false"/> from the moment it is dialed until it
    /// answers. A call whose party has not answered cannot be merged. <see langword="null"/> on a leg that predates this.
    /// </summary>
    [JsonPropertyName("j")]
    public bool? PeerAnswered { get; set; }

    /// <summary>
    /// Gets or sets a leg to hang up when this one ends, for two outside parties the platform joined to each other and
    /// then left: nothing else ties them together.
    /// </summary>
    [JsonPropertyName("w")]
    public string ReleaseWithCallControlId { get; set; }

    /// <summary>
    /// Gets or sets the Contact Center consult a consult leg was placed for (consult-leg state only).
    /// </summary>
    [JsonPropertyName("k")]
    public string ConsultId { get; set; }

    /// <summary>
    /// Gets or sets the leg intent, one of <see cref="AgentLegIntent"/> or <see cref="DestinationLegIntent"/>.
    /// </summary>
    [JsonPropertyName("i")]
    public string Intent { get; set; }

    /// <summary>
    /// Gets or sets the omnichannel activity identifier an automated AI voice leg fulfills (AI-voice state only).
    /// The webhook loop resolves the activity, its AI session, and its AI profile from this id.
    /// </summary>
    [JsonPropertyName("a")]
    public string ActivityId { get; set; }

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
    /// Gets or sets the caller display name to present to the destination, so a callee ringing on an internal
    /// extension call sees who is calling rather than only the caller-id number (agent-leg state only, carried to
    /// the destination leg's <c>from_display_name</c>).
    /// </summary>
    [JsonPropertyName("n")]
    public string CallerDisplayName { get; set; }

    /// <summary>
    /// Gets or sets the agent leg's call control id to bridge to once the destination answers
    /// (destination-leg state only).
    /// </summary>
    [JsonPropertyName("p")]
    public string PeerCallControlId { get; set; }

    /// <summary>
    /// Gets or sets the conference name used when adding an internal extension into an active call
    /// (conference-extension-leg state only).
    /// </summary>
    [JsonPropertyName("c")]
    public string ConferenceName { get; set; }

    /// <summary>
    /// Gets or sets the user id whose voicemail an unanswered internal extension call is routed to. When set on
    /// an agent/destination leg, a no-answer hangup of the destination leg sends the caller to this user's
    /// voicemail instead of simply ending the call.
    /// </summary>
    [JsonPropertyName("v")]
    public string VoicemailRecipientUserId { get; set; }

    /// <summary>
    /// Gets or sets the ring window, in seconds, for the destination leg of an internal extension call. After
    /// this window the provider stops ringing the target so the caller can be routed to voicemail.
    /// </summary>
    [JsonPropertyName("t")]
    public int? RingTimeoutSeconds { get; set; }

    /// <summary>
    /// Gets or sets the Contact Center offer (reservation) a pre-dialed agent leg was rung for (pre-dialed agent-leg
    /// state only). The agent's browser reads it too, to tie the incoming leg to the offer it is showing.
    /// </summary>
    [JsonPropertyName("r")]
    public string ReservationId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the leg no longer belongs to the leg in <see cref="PeerCallControlId"/>:
    /// the platform transferred it, moved it into a conference, or released it on purpose. The end of a detached leg
    /// says nothing about its former partner, so it is never hung up with it.
    /// </summary>
    [JsonPropertyName("x")]
    public bool? Detached { get; set; }

    /// <summary>
    /// Gets or sets, on a leg that rings a user's browser for the platform (a Contact Center agent leg), the user whose
    /// phone it rings, so a leg refused as unavailable can be rung again on the credential that phone moved to.
    /// </summary>
    [JsonPropertyName("b")]
    public string RingUserId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this leg already replaces one that was refused as unavailable. A leg is
    /// rung again at most once, so a phone that is really gone ends the call instead of being rung in a loop.
    /// </summary>
    [JsonPropertyName("e")]
    public bool? Redelivered { get; set; }

    /// <summary>
    /// Gets a value indicating whether this is the agent leg of a number dialed from the soft phone and connected on
    /// the server, once the number's leg exists: <see cref="PeerCallControlId"/> is then the remote party.
    /// </summary>
    [JsonIgnore]
    public bool IsBridgedDialAgentLeg
        => Intent == AgentLegIntent &&
            string.IsNullOrWhiteSpace(VoicemailRecipientUserId) &&
            !string.IsNullOrWhiteSpace(PeerCallControlId);

    /// <summary>
    /// Gets a value indicating whether this is the caller's leg of an internal extension call once the colleague's leg
    /// exists: <see cref="PeerCallControlId"/> is then the colleague, joined with this leg in the conference
    /// <c>ext-{this leg}</c>.
    /// </summary>
    [JsonIgnore]
    public bool IsExtensionAgentLeg
        => Intent == AgentLegIntent &&
            !string.IsNullOrWhiteSpace(VoicemailRecipientUserId) &&
            !string.IsNullOrWhiteSpace(PeerCallControlId);

    /// <summary>
    /// Gets a value indicating whether this is the agent leg rung for a warm transfer's consult.
    /// </summary>
    [JsonIgnore]
    public bool IsConsultAgentLeg
        => Intent == AgentLegIntent && !string.IsNullOrWhiteSpace(ConsultOfCallControlId);

    /// <summary>
    /// Returns a copy of this state marked <see cref="Detached"/>.
    /// </summary>
    public TelnyxOutboundBridgeState AsDetached()
    {
        var copy = (TelnyxOutboundBridgeState)MemberwiseClone();
        copy.Detached = true;
        copy.PendingTransferCallControlId = null;

        return copy;
    }

    /// <summary>
    /// Returns a copy of this state marked <see cref="Redelivered"/>, for the leg that replaces one refused as unavailable.
    /// </summary>
    public TelnyxOutboundBridgeState AsRedelivered()
    {
        var copy = (TelnyxOutboundBridgeState)MemberwiseClone();
        copy.Redelivered = true;

        return copy;
    }

    /// <summary>
    /// Returns a copy of this state that names <paramref name="pendingTransferCallControlId"/> as the transfer under way,
    /// or none.
    /// </summary>
    /// <param name="pendingTransferCallControlId">The leg the transfer rang, or <see langword="null"/>.</param>
    public TelnyxOutboundBridgeState WithPendingTransfer(string pendingTransferCallControlId)
    {
        var copy = (TelnyxOutboundBridgeState)MemberwiseClone();
        copy.PendingTransferCallControlId = pendingTransferCallControlId;

        return copy;
    }

    /// <summary>
    /// Returns a copy of this state that names <paramref name="peerCallControlId"/> as the leg it is connected to.
    /// </summary>
    /// <param name="peerCallControlId">The other leg.</param>
    public TelnyxOutboundBridgeState WithPeer(string peerCallControlId)
    {
        var copy = (TelnyxOutboundBridgeState)MemberwiseClone();
        copy.PeerCallControlId = peerCallControlId;

        return copy;
    }

    /// <summary>
    /// Attempts to parse a <c>client_state</c> value exactly as Telnyx returns it (base64-encoded).
    /// </summary>
    /// <param name="encodedClientState">The base64 value.</param>
    /// <param name="state">The parsed state when successful.</param>
    /// <returns><see langword="true"/> when the value is one of this feature's outbound-bridge states.</returns>
    public static bool TryParseEncoded(string encodedClientState, out TelnyxOutboundBridgeState state)
    {
        state = null;

        if (string.IsNullOrWhiteSpace(encodedClientState))
        {
            return false;
        }

        try
        {
            return TryParse(Encoding.UTF8.GetString(Convert.FromBase64String(encodedClientState.Trim())), out state);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Serializes the state to the base64 form Telnyx expects for a <c>client_state</c> value.
    /// </summary>
    public string ToClientState()
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(ToClientStateJson()));

    /// <summary>
    /// Serializes the state without encoding it, for callers that hand it to something which owns the
    /// base64 transport encoding itself. Encoding twice produces a value Telnyx echoes back that decodes to
    /// base64 rather than to JSON, so every correlation silently fails to parse.
    /// </summary>
    /// <returns>The state as JSON.</returns>
    public string ToClientStateJson()
        => JsonSerializer.Serialize(this, _options);

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
                (parsed.Intent != AgentLegIntent &&
                 parsed.Intent != DestinationLegIntent &&
                 parsed.Intent != ContactCenterAgentLegIntent &&
                 parsed.Intent != ContactCenterPreDialedAgentLegIntent &&
                 parsed.Intent != ConferenceExtensionLegIntent &&
                 parsed.Intent != AiVoiceLegIntent &&
                 parsed.Intent != ContactCenterConsultLegIntent &&
                 parsed.Intent != TransferLegIntent &&
                 parsed.Intent != ContactCenterSupervisorLegIntent))
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
