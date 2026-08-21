using System.ComponentModel.DataAnnotations;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// Represents the edit view model for an inbound entry point.
/// </summary>
public class EntryPointViewModel
{
    /// <summary>
    /// Gets or sets the entry point identifier.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the unique entry point name.
    /// </summary>
    [Required]
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the entry point description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the dialed numbers, one per line.
    /// </summary>
    public string DialedNumbersText { get; set; }

    /// <summary>
    /// Gets or sets what the entry point routes calls to: a queue or a specific agent.
    /// </summary>
    public EntryPointTargetType TargetType { get; set; } = EntryPointTargetType.Queue;

    /// <summary>
    /// Gets or sets the target agent identifier used when <see cref="TargetType"/> is
    /// <see cref="EntryPointTargetType.Agent"/>.
    /// </summary>
    public string TargetAgentId { get; set; }

    /// <summary>
    /// Gets or sets the available target agents.
    /// </summary>
    public IList<SelectListItem> TargetAgentOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the target queue identifier. For an agent target this is the fallback queue.
    /// </summary>
    public string TargetQueueId { get; set; }

    /// <summary>
    /// Gets or sets the available target queues.
    /// </summary>
    public IList<SelectListItem> TargetQueueOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the call priority.
    /// </summary>
    public InteractionPriority Priority { get; set; } = InteractionPriority.Normal;

    /// <summary>
    /// Gets or sets a value indicating whether an unanswered specific-agent call goes to voicemail. When off,
    /// the caller keeps ringing and is held for the agent.
    /// </summary>
    public bool VoicemailEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the ring window, in seconds, for a specific-agent target: how long the caller rings and is
    /// held for the agent before going to voicemail. Only used when <see cref="VoicemailEnabled"/> is on.
    /// </summary>
    public int RingTimeoutSeconds { get; set; } = ContactCenterConstants.DirectRouting.DefaultRingTimeoutSeconds;

    /// <summary>
    /// Gets or sets the business-hours calendar identifier.
    /// </summary>
    public string BusinessHoursCalendarId { get; set; }

    /// <summary>
    /// Gets or sets the available business-hours calendars.
    /// </summary>
    public IList<SelectListItem> BusinessHoursCalendarOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the closed action.
    /// </summary>
    public EntryPointClosedAction ClosedAction { get; set; } = EntryPointClosedAction.HoldInQueue;

    /// <summary>
    /// Gets or sets the overflow queue identifier.
    /// </summary>
    public string OverflowQueueId { get; set; }

    /// <summary>
    /// Gets or sets the available overflow queues.
    /// </summary>
    public IList<SelectListItem> OverflowQueueOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the welcome message.
    /// </summary>
    public string WelcomeMessage { get; set; }

    /// <summary>
    /// Gets or sets the closed message.
    /// </summary>
    public string ClosedMessage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the entry point is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
