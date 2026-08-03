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
    }
}
