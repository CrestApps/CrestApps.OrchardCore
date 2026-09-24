namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Who made a change the event log records: the audit has to tell an agent's own choice from one a supervisor, a
/// workflow or the platform made for them.
/// </summary>
public enum ContactCenterActorType
{
    /// <summary>
    /// Not recorded, as on events written before the actor was captured.
    /// </summary>
    Unspecified,

    /// <summary>
    /// The agent the change is about, acting for themselves.
    /// </summary>
    Agent,

    /// <summary>
    /// A supervisor acting on an agent or a call.
    /// </summary>
    Supervisor,

    /// <summary>
    /// The platform: routing, a sweep, a timeout or reconciliation.
    /// </summary>
    System,

    /// <summary>
    /// A workflow.
    /// </summary>
    Workflow,

    /// <summary>
    /// The telephony provider, reporting something that happened on the call.
    /// </summary>
    Provider,

    /// <summary>
    /// The customer, such as a caller hanging up.
    /// </summary>
    Customer,

    /// <summary>
    /// An automated voice agent.
    /// </summary>
    AiAgent,
}
