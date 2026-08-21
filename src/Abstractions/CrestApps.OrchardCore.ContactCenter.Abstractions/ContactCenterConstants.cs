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
