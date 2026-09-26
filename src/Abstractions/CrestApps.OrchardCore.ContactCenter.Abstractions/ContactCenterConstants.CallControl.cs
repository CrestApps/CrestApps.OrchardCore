namespace CrestApps.OrchardCore.ContactCenter;

public static partial class ContactCenterConstants
{
    /// <summary>
    /// Contains stable metadata keys shared across Contact Center command boundaries.
    /// </summary>
    public static class CommandMetadata
    {
        /// <summary>
        /// Identifies the idempotent provider command associated with an interaction.
        /// </summary>
        public const string CommandId = "providerCommandId";

        /// <summary>
        /// Identifies the monotonic fence token for the current provider-command claim.
        /// </summary>
        public const string FenceToken = "providerCommandFence";

        /// <summary>
        /// Identifies the interaction a provider command acts on. Providers use it to correlate side effects
        /// (for example a voicemail recording) back to the owning interaction.
        /// </summary>
        public const string InteractionId = "providerCommandInteractionId";
    }

    /// <summary>
    /// Contains stable request-metadata keys the transfer service passes to a voice provider so the provider can
    /// execute a resolved transfer destination without receiving raw client input.
    /// </summary>
    public static class TransferMetadata
    {
        /// <summary>
        /// Identifies the Orchard user id of the destination agent for an agent transfer, so a provider can resolve
        /// that agent's live endpoint. The client never supplies this; the transfer service resolves it server-side.
        /// </summary>
        public const string AgentUserId = "transferAgentUserId";
    }

    /// <summary>
    /// Contains stable request-metadata keys the conference service passes to a voice provider so the provider can
    /// add a resolved conference participant without receiving raw client input.
    /// </summary>
    public static class ConferenceMetadata
    {
        /// <summary>
        /// Identifies the Orchard user id of the agent to add to a live conversation as a conference participant, so
        /// a provider can resolve that agent's live endpoint. The client never supplies this; the conference service
        /// resolves it server-side.
        /// </summary>
        public const string AgentUserId = "conferenceAgentUserId";
    }

    /// <summary>
    /// Contains stable request- and result-metadata keys used to drive an attended (consultative) transfer across
    /// its begin, complete, and cancel phases so a provider can execute a resolved consult without receiving raw
    /// client input.
    /// </summary>
    public static class AttendedTransferMetadata
    {
        /// <summary>
        /// Identifies the Orchard user id of the destination agent to consult with, so a provider can resolve that
        /// agent's live endpoint. The client never supplies this; the transfer service resolves it server-side.
        /// </summary>
        public const string AgentUserId = "attendedTransferAgentUserId";

        /// <summary>
        /// Identifies the provider leg of the agent who is consulting, which the provider moves next to the
        /// destination while the customer is held, and drops when the transfer completes.
        /// </summary>
        public const string AgentLegId = "attendedTransferAgentLegId";

        /// <summary>
        /// The kind of destination being consulted (<c>Agent</c> or <c>External</c>), which decides how a provider
        /// joins the customer to the destination on completion.
        /// </summary>
        public const string TargetType = "attendedTransferTargetType";

        /// <summary>
        /// The audio the customer hears while held for the consult: a URL, or a clip the provider already stores.
        /// </summary>
        public const string HoldAudio = "attendedTransferHoldAudio";

        /// <summary>
        /// Says who ended a cancelled consult, and therefore what is left for the provider to undo.
        /// </summary>
        public const string EndedBy = "attendedTransferEndedBy";

        /// <summary>The agent cancelled: drop the destination and return the customer.</summary>
        public const string EndedByAgent = "agent";

        /// <summary>The destination hung up or never answered: return the customer.</summary>
        public const string EndedByTarget = "target";

        /// <summary>The customer hung up: drop the destination.</summary>
        public const string EndedByCaller = "caller";
    }

    /// <summary>
    /// Contains stable metadata keys written to call sessions and interactions for provider-reported telephony details.
    /// </summary>
    public static class TelephonyMetadata
    {
        /// <summary>
        /// The key under which the AMD (Answering Machine Detection) answer classification is stored.
        /// </summary>
        public const string AnswerClassification = "amd_answer_classification";

        /// <summary>
        /// The key under which a provider reports its own hangup cause, unchanged, on a voice event.
        /// </summary>
        public const string ProviderHangupCause = "provider_hangup_cause";

        /// <summary>
        /// The key under which a provider reports the SIP response code that ended a call, on a voice event.
        /// </summary>
        public const string SipHangupCause = "sip_hangup_cause";

        /// <summary>
        /// The key under which a provider reports who ended a call, such as the caller or the callee, on a voice
        /// event.
        /// </summary>
        public const string HangupSource = "hangup_source";

        /// <summary>
        /// The key under which an inbound call's interaction keeps the number the caller dialled: the contact
        /// center's own line, which is what a call sent on from that line presents as its caller.
        /// </summary>
        public const string ServiceAddress = "serviceAddress";
    }
}
