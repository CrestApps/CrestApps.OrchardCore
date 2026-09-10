namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Contains constant values shared across the Contact Center module set.
/// </summary>
public static partial class ContactCenterConstants
{
    /// <summary>
    /// Identifies a system actor for events that are not originated by an interactive user.
    /// </summary>
    public const string SystemActor = "system";

    /// <summary>
    /// Determines whether the supplied queue identifier is the synthetic direct-routing queue used to carry a
    /// direct-to-agent (personal line) inbound call through the reservation and offer pipeline.
    /// </summary>
    /// <param name="queueId">The queue identifier to test.</param>
    /// <returns><see langword="true"/> when the identifier is the direct-routing queue; otherwise <see langword="false"/>.</returns>
    public static bool IsDirectRoutingQueue(string queueId)
        => string.Equals(queueId, DirectRouting.QueueId, StringComparison.Ordinal);

    /// <summary>
    /// Determines whether the supplied queue identifier is a virtual campaign queue that carries a dialer
    /// campaign's outbound work. A campaign queue is never stored as an <c>ActivityQueue</c> record; it is
    /// synthesized on demand by the router from the campaign, so it never appears in queue administration or
    /// any queue picker. Agents join it implicitly by signing into the campaign.
    /// </summary>
    /// <param name="queueId">The queue identifier to test.</param>
    /// <returns><see langword="true"/> when the identifier is a virtual campaign queue; otherwise <see langword="false"/>.</returns>
    public static bool IsCampaignQueue(string queueId)
        => queueId is not null && queueId.StartsWith(CampaignQueue.Prefix, StringComparison.Ordinal);

    /// <summary>
    /// Contains the well-known values for the virtual per-campaign outbound queue. Outbound campaign work is
    /// routed under a queue identifier derived from the campaign; the queue itself is never persisted, so it is
    /// invisible to users and requires no configuration.
    /// </summary>
    public static class CampaignQueue
    {
        /// <summary>
        /// The prefix that identifies a virtual campaign queue identifier.
        /// </summary>
        public const string Prefix = "__campaign-queue__";

        /// <summary>
        /// Builds the virtual queue identifier a campaign's outbound work is routed under.
        /// </summary>
        /// <param name="campaignId">The campaign content item identifier.</param>
        /// <returns>The derived campaign queue identifier.</returns>
        public static string CreateId(string campaignId)
            => Prefix + campaignId;

        /// <summary>
        /// Extracts the campaign identifier from a virtual campaign queue identifier.
        /// </summary>
        /// <param name="queueId">The campaign queue identifier.</param>
        /// <returns>The campaign identifier, or <see langword="null"/> when the identifier is not a campaign queue.</returns>
        public static string GetCampaignId(string queueId)
            => queueId is not null && queueId.StartsWith(Prefix, StringComparison.Ordinal)
                ? queueId[Prefix.Length..]
                : null;
    }

    /// <summary>
    /// Determines whether a call carried under the supplied queue identifier drives automatic after-call work
    /// (wrap-up) for the handling agent when it ends.
    /// </summary>
    /// <remarks>
    /// Wrap-up is reserved for ACD-routed queue and campaign work, where the platform manages the agent's status
    /// so they can document and disposition the interaction before the router assigns the next one. A
    /// direct-to-agent (personal line) call is handled like a manual call: there is nothing to disposition, so the
    /// agent returns straight to their ready state instead of being parked in wrap-up. Only the synthetic
    /// direct-routing queue is excluded; a real queue (including a campaign's queue) and an unqueued
    /// system-handled call remain wrap-up eligible.
    /// </remarks>
    /// <param name="queueId">The queue identifier the call was routed under.</param>
    /// <returns><see langword="true"/> when a handled call under the queue starts after-call work; otherwise <see langword="false"/>.</returns>
    public static bool QueueStartsAfterCallWork(string queueId)
        => !IsDirectRoutingQueue(queueId);

    /// <summary>
    /// Contains the well-known values for direct-to-agent (personal line) routing. A specific-agent entry point
    /// rings one agent directly rather than a queue: the call is carried through the existing reservation and
    /// offer pipeline under this synthetic queue identifier, which has no persisted queue row and therefore never
    /// appears in queue administration, agent sign-in, deployment exports, or the background assignment sweep.
    /// </summary>
    public static class DirectRouting
    {
        /// <summary>
        /// The synthetic queue identifier a direct-to-agent inbound call is reserved and offered under. It is
        /// intentionally not a real <c>ActivityQueue</c> item id so no agent can sign into it and no routing
        /// strategy can offer the call to anyone other than the named agent.
        /// </summary>
        public const string QueueId = "__cc-direct-routing__";

        /// <summary>
        /// The interaction technical-metadata key that records the agent profile a held direct-to-agent call is
        /// waiting for. It lets a call held while the agent was unavailable be re-offered to that same agent
        /// when they become available, rather than to anyone else.
        /// </summary>
        public const string TargetAgentMetadataKey = "directTargetAgentId";

        /// <summary>
        /// The interaction technical-metadata key that records the ring window, in seconds, configured on the
        /// entry point for a direct-to-agent call. It bounds how long the caller waits (ringing and held) before
        /// being sent to the agent's voicemail, and is read by both the offer (reservation) timeout and the
        /// held-call timeout sweep.
        /// </summary>
        public const string RingTimeoutMetadataKey = "directRingTimeoutSeconds";

        /// <summary>
        /// The default ring window, in seconds, for a direct-to-agent entry point when none is configured.
        /// </summary>
        public const int DefaultRingTimeoutSeconds = 30;

        /// <summary>
        /// The smallest ring window, in seconds, a direct-to-agent entry point may be configured with.
        /// </summary>
        public const int MinimumRingTimeoutSeconds = 5;

        /// <summary>
        /// The largest ring window, in seconds, a direct-to-agent entry point may be configured with.
        /// </summary>
        public const int MaximumRingTimeoutSeconds = 300;
    }

    /// <summary>
    /// Contains the well-known values used to project a call that is being sent to an agent's voicemail back onto
    /// that agent's soft phone. When a call rings an agent and is then sent to voicemail (the agent let the offer
    /// expire, the entry point was closed, or a held direct call timed out), the provider leg is answered by the
    /// platform to record the message. That answer must not surface on the target agent's soft phone as a live
    /// "in call" state: the agent never took the call, so it is a missed call.
    /// </summary>
    public static class Voicemail
    {
        /// <summary>
        /// The interaction technical-metadata key that flags an interaction as being sent to voicemail. While set,
        /// the soft-phone projection renders the call to the recipient agent as a terminal, missed call rather than
        /// a live call, and the platform-answered recording leg never reactivates the agent's soft phone.
        /// </summary>
        public const string ProjectionMetadataKey = "agentVoicemailProjection";

        /// <summary>
        /// The interaction technical-metadata key that records the agent-profile identifier of the voicemail
        /// recipient, so the soft-phone projection can resolve the target agent even after the live reservation and
        /// call session have released their agent association.
        /// </summary>
        public const string RecipientAgentMetadataKey = "agentVoicemailRecipientAgentId";

        /// <summary>
        /// The command-metadata key that carries the recipient agent's text-to-speech voicemail greeting to the
        /// telephony provider, so the provider speaks the per-agent greeting before recording rather than a
        /// hard-coded default.
        /// </summary>
        public const string GreetingTextMetadataKey = "voicemailGreetingText";

        /// <summary>
        /// The command-metadata key that carries the absolute URL of the recipient agent's recorded/uploaded audio
        /// greeting to the telephony provider. When present it overrides <see cref="GreetingTextMetadataKey"/>: the
        /// provider plays the audio file before recording instead of speaking the text.
        /// </summary>
        public const string GreetingMediaUrlMetadataKey = "voicemailGreetingMediaUrl";

        /// <summary>
        /// The command-metadata key that carries the provider-hosted media reference (for Telnyx, the Media Storage
        /// <c>media_name</c>) of the recipient agent's recorded/uploaded audio greeting. When present it is preferred
        /// over both <see cref="GreetingMediaUrlMetadataKey"/> and <see cref="GreetingTextMetadataKey"/>: the provider
        /// plays its own hosted copy before recording, so no publicly reachable URL of ours is required.
        /// </summary>
        public const string GreetingMediaNameMetadataKey = "voicemailGreetingMediaName";

        /// <summary>
        /// The interaction-metadata key that carries the entry point's default (fallback) spoken greeting, stamped
        /// when the inbound call is created. It is used when the recipient agent has not set their own greeting, so
        /// a dialed number can define the message its callers hear without each agent configuring one.
        /// </summary>
        public const string EntryPointGreetingTextMetadataKey = "voicemailEntryPointGreetingText";
    }

    /// <summary>
    /// Contains the stable <c>AggregateType</c> discriminators that are not derived from a public domain-model
    /// type name. These values are emitted on published <c>InteractionEvent</c> instances and therefore form
    /// part of the module's public event contract that webhook and workflow consumers may inspect.
    /// </summary>
    public static class AggregateTypes
    {
        /// <summary>
        /// The event aggregate type used for a manual, agent-initiated soft-phone call, which is not part of a
        /// campaign and therefore has no dialer profile or CRM activity to anchor the event to.
        /// </summary>
        public const string ManualCall = "ManualCall";
    }

    /// <summary>
    /// Contains the well-known names of the Contact Center components that originate domain events.
    /// </summary>
    public static class Components
    {
        /// <summary>
        /// The interaction management component.
        /// </summary>
        public const string Interactions = "Interactions";

        /// <summary>
        /// The queue management component.
        /// </summary>
        public const string Queues = "Queues";

        /// <summary>
        /// The routing engine component.
        /// </summary>
        public const string Routing = "Routing";

        /// <summary>
        /// The agent and presence management component.
        /// </summary>
        public const string Agents = "Agents";

        /// <summary>
        /// The optional agent entitlement component. When enabled, an agent may sign in only to the queues and
        /// campaigns explicitly granted on their profile; when disabled, any agent may sign in to any queue or
        /// campaign with no per-agent setup.
        /// </summary>
        public const string AgentEntitlements = "AgentEntitlements";

        /// <summary>
        /// The voice channel adapter component.
        /// </summary>
        public const string Voice = "Voice";

        /// <summary>
        /// The outbound dialer component.
        /// </summary>
        public const string Dialer = "Dialer";

        /// <summary>
        /// The call session management component.
        /// </summary>
        public const string CallSessions = "CallSessions";

        /// <summary>
        /// The real-time agent and supervisor experience component.
        /// </summary>
        public const string RealTime = "RealTime";

        /// <summary>
        /// The agent-assisted secure data capture component.
        /// </summary>
        public const string SecureCapture = "SecureCapture";
    }
}
