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
    /// <see cref="TargetType"/> is <see cref="EntryPointTargetType.Agent"/>. The call rings that agent
    /// directly (a personal line); there is no queue fallback. When the agent cannot take the call it is sent
    /// to that agent's voicemail.
    /// </summary>
    public string TargetAgentId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the queue calls route to while the entry point is open, when
    /// <see cref="TargetType"/> is <see cref="EntryPointTargetType.Queue"/>. It is not used for an
    /// agent-target entry point.
    /// </summary>
    public string TargetQueueId { get; set; }

    /// <summary>
    /// Gets or sets the priority assigned to calls entering through this entry point.
    /// </summary>
    public InteractionPriority Priority { get; set; } = InteractionPriority.Normal;

    /// <summary>
    /// Gets or sets a value indicating whether an unanswered direct-to-agent call is sent to the agent's
    /// voicemail. When disabled the caller keeps ringing and is held for the agent until answered or they hang
    /// up. Only applies when <see cref="TargetType"/> is <see cref="EntryPointTargetType.Agent"/>.
    /// </summary>
    public bool VoicemailEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the ring window, in seconds, for a direct-to-agent target: how long the caller rings and is
    /// held waiting for the named agent before being sent to that agent's voicemail. Only applies when
    /// <see cref="TargetType"/> is <see cref="EntryPointTargetType.Agent"/> and <see cref="VoicemailEnabled"/>
    /// is <see langword="true"/>.
    /// </summary>
    public int RingTimeoutSeconds { get; set; } = ContactCenterConstants.DirectRouting.DefaultRingTimeoutSeconds;

    /// <summary>
    /// Gets or sets the default spoken (text-to-speech) voicemail greeting for calls that arrive through this
    /// entry point. It is the fallback used when the agent the caller reaches has not recorded their own greeting;
    /// when this is also empty, the built-in system greeting is used. This is configured for the agents (per
    /// dialed number / entry point), not by them.
    /// </summary>
    public string VoicemailGreetingText { get; set; }

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
