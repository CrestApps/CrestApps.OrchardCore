using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents an inbound entry point: it maps one or more dialed numbers (DIDs) to a target queue,
/// gates the call by a business-hours calendar, and defines what happens while the entry point is closed.
/// </summary>
public sealed class ContactCenterEntryPoint : CatalogItem, INameAwareModel, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the unique name of the entry point.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the description of the entry point.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the dialed numbers (DIDs) served by this entry point.
    /// </summary>
    public IList<string> DialedNumbers { get; set; } = [];

    /// <summary>
    /// Gets or sets what the entry point routes calls to: a queue (default) or a specific agent.
    /// </summary>
    public EntryPointTargetType TargetType { get; set; } = EntryPointTargetType.Queue;

    /// <summary>
    /// Gets or sets the identifier of the agent profile calls route directly to when
    /// <see cref="TargetType"/> is <see cref="EntryPointTargetType.Agent"/>. When that agent is
    /// unavailable, the call falls back to <see cref="TargetQueueId"/>.
    /// </summary>
    public string TargetAgentId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the queue calls route to while the entry point is open. For an
    /// agent-target entry point this is the fallback queue used when the agent is unavailable.
    /// </summary>
    public string TargetQueueId { get; set; }

    /// <summary>
    /// Gets or sets the priority assigned to calls entering through this entry point.
    /// </summary>
    public InteractionPriority Priority { get; set; } = InteractionPriority.Normal;

    /// <summary>
    /// Gets or sets the identifier of the business-hours calendar that gates when the entry point is open.
    /// When empty, the entry point is always open.
    /// </summary>
    public string BusinessHoursCalendarId { get; set; }

    /// <summary>
    /// Gets or sets the action taken for calls while the entry point is closed.
    /// </summary>
    public EntryPointClosedAction ClosedAction { get; set; } = EntryPointClosedAction.HoldInQueue;

    /// <summary>
    /// Gets or sets the identifier of the queue used when <see cref="ClosedAction"/> is
    /// <see cref="EntryPointClosedAction.Overflow"/>.
    /// </summary>
    public string OverflowQueueId { get; set; }

    /// <summary>
    /// Gets or sets the greeting or announcement shown to the caller.
    /// </summary>
    public string WelcomeMessage { get; set; }

    /// <summary>
    /// Gets or sets the message played when the entry point is closed.
    /// </summary>
    public string ClosedMessage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the entry point is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the UTC time the entry point was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the entry point was last modified.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
