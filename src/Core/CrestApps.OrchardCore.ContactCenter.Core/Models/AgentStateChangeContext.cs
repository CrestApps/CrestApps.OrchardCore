namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Who made an agent state change, why, and what it was about: what a writer knows about a transition beyond the
/// state it moves the agent to.
/// </summary>
/// <remarks>
/// Every member is optional. A writer fills in what it knows, and the presence manager or the transition service
/// supplies the rest (the actor defaults to the platform, the time to now).
/// </remarks>
public sealed class AgentStateChangeContext
{
    /// <summary>
    /// Gets or sets who made the change. <see langword="null"/> means the platform.
    /// </summary>
    public ContactCenterActor Actor { get; set; }

    /// <summary>
    /// Gets or sets what caused the change, as one of <see cref="AgentStateChangeSources"/>. <see langword="null"/>
    /// lets the writer choose its usual source.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the configured reason code chosen for the new state.
    /// </summary>
    public string ReasonCodeId { get; set; }

    /// <summary>
    /// Gets or sets the reason for the new state as it reads now: the reason code's name, or free text.
    /// </summary>
    public string ReasonName { get; set; }

    /// <summary>
    /// Gets or sets why a reservation ended without its work being taken, as one of
    /// <see cref="AgentReleaseReasons"/>.
    /// </summary>
    public string ReleaseReason { get; set; }

    /// <summary>
    /// Gets or sets the interaction the change is about.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the reservation the change is about.
    /// </summary>
    public string ReservationId { get; set; }

    /// <summary>
    /// Gets or sets the agent session the change belongs to.
    /// </summary>
    public string AgentSessionId { get; set; }

    /// <summary>
    /// Gets or sets when the change took effect, when that is not now: for a sign-off after lost contact, the last
    /// moment the agent was heard from.
    /// </summary>
    public DateTime? ChangedUtc { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the change is recorded even though the state stays the same, as when
    /// an agent on a break switches to a break with a different reason.
    /// </summary>
    public bool RecordWhenUnchanged { get; set; }
}
