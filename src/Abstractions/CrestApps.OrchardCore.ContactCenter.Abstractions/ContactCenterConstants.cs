namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Contains constant values shared across the Contact Center module set.
/// </summary>
public static class ContactCenterConstants
{
    /// <summary>
    /// The YesSql collection name used to store Contact Center documents in isolation from other modules.
    /// </summary>
    public const string CollectionName = "ContactCenter";

    /// <summary>
    /// The current schema version applied to newly published Contact Center domain events.
    /// </summary>
    public const int CurrentEventSchemaVersion = 1;

    /// <summary>
    /// Identifies a system actor for events that are not originated by an interactive user.
    /// </summary>
    public const string SystemActor = "system";

    /// <summary>
    /// Contains the feature identifiers exposed by the Contact Center module set.
    /// </summary>
    public static class Feature
    {
        /// <summary>
        /// The identifier of the base Contact Center feature.
        /// </summary>
        public const string Area = "CrestApps.OrchardCore.ContactCenter";
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
        /// The wrap-up and disposition component.
        /// </summary>
        public const string WrapUp = "WrapUp";
    }

    /// <summary>
    /// Contains the canonical Contact Center domain event type names.
    /// Names are channel-neutral and stable so they can be persisted, projected, and replayed.
    /// </summary>
    public static class Events
    {
        /// <summary>
        /// Raised when a new interaction is created.
        /// </summary>
        public const string InteractionCreated = "InteractionCreated";

        /// <summary>
        /// Raised when an interaction is linked to a CRM activity.
        /// </summary>
        public const string InteractionLinkedToActivity = "InteractionLinkedToActivity";

        /// <summary>
        /// Raised when an activity is reserved by routing, a dialer, or an agent.
        /// </summary>
        public const string ActivityReserved = "ActivityReserved";

        /// <summary>
        /// Raised when an activity assignment changes.
        /// </summary>
        public const string ActivityAssignmentChanged = "ActivityAssignmentChanged";

        /// <summary>
        /// Raised when an activity disposition is applied.
        /// </summary>
        public const string ActivityDispositionApplied = "ActivityDispositionApplied";

        /// <summary>
        /// Raised when the communication session for an interaction starts.
        /// </summary>
        public const string InteractionStarted = "InteractionStarted";

        /// <summary>
        /// Raised when an interaction is updated.
        /// </summary>
        public const string InteractionUpdated = "InteractionUpdated";

        /// <summary>
        /// Raised when an interaction is transferred.
        /// </summary>
        public const string InteractionTransferred = "InteractionTransferred";

        /// <summary>
        /// Raised when the communication session for an interaction ends.
        /// </summary>
        public const string InteractionEnded = "InteractionEnded";

        /// <summary>
        /// Raised when an interaction fails.
        /// </summary>
        public const string InteractionFailed = "InteractionFailed";
    }
}
