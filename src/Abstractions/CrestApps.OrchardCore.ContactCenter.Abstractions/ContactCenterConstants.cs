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
