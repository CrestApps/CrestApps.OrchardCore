using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// What kind of work an agent is wrongly holding.
/// </summary>
public enum StaleAgentWorkKind
{
    /// <summary>
    /// A soft-phone call record that is still in progress although the provider cannot resolve the call, or nothing is
    /// left of the browser session that placed it.
    /// </summary>
    TelephonyCall,

    /// <summary>
    /// A Contact Center interaction that is not settled although the provider reports its call gone.
    /// </summary>
    ProviderInteraction,

    /// <summary>
    /// A Reserved or Busy presence with no work behind it.
    /// </summary>
    Presence,

    /// <summary>
    /// A reservation the agent still holds that no live offer or call backs.
    /// </summary>
    Reservation,

    /// <summary>
    /// A queue item reserved for, or assigned to, the agent that no live offer or call backs.
    /// </summary>
    QueueItem,
}

/// <summary>
/// One piece of stale work an agent holds.
/// </summary>
public sealed class StaleAgentWorkItem
{
    /// <summary>
    /// Gets or sets what kind of work this is.
    /// </summary>
    public StaleAgentWorkKind Kind { get; set; }

    /// <summary>
    /// Gets or sets a short, generic reason the work is considered stale.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the record behind the item (a call, interaction, reservation or queue item), when
    /// it has one.
    /// </summary>
    public string ReferenceId { get; set; }

    /// <summary>
    /// Gets or sets when the work began, when that is known, so a supervisor can see how long it has been stuck.
    /// </summary>
    public DateTime? StartedUtc { get; set; }
}

/// <summary>
/// What is stale for one agent, as found by <c>IStaleAgentWorkService</c>.
/// </summary>
public sealed class StaleAgentWork
{
    /// <summary>
    /// Gets or sets the agent profile identifier.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets the stale items found for the agent.
    /// </summary>
    public IList<StaleAgentWorkItem> Items { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the agent also holds work that is not provably stale, such as a call the
    /// provider confirms is still alive. Releasing the agent's stale work is refused while it does.
    /// </summary>
    public bool HasLiveWork { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the provider confirmed a call of the agent is still alive.
    /// </summary>
    public bool ProviderConfirmsLiveCall { get; set; }

    /// <summary>
    /// Gets a value indicating whether anything stale was found.
    /// </summary>
    public bool IsStale => Items.Count > 0;
}

/// <summary>
/// The outcome of releasing an agent's stale work.
/// </summary>
public sealed class StaleAgentWorkRelease
{
    /// <summary>
    /// Gets or sets a value indicating whether the agent's stale work was released.
    /// </summary>
    public bool Released { get; set; }

    /// <summary>
    /// Gets or sets why nothing was released, or a note about what was.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the release was declined because the agent holds a live call.
    /// </summary>
    public bool DeclinedForLiveCall { get; set; }

    /// <summary>
    /// Gets or sets what was released.
    /// </summary>
    public IList<StaleAgentWorkItem> ReleasedItems { get; set; } = [];

    /// <summary>
    /// Gets or sets the state the agent was returned to, when they were.
    /// </summary>
    public AgentPresenceStatus? ReturnedTo { get; set; }
}
