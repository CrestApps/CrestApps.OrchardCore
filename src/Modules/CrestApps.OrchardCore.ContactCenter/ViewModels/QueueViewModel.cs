using System.ComponentModel.DataAnnotations;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// Represents the edit view model for an activity queue.
/// </summary>
public class QueueViewModel
{
    /// <summary>
    /// Gets or sets the queue identifier.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the unique queue name.
    /// </summary>
    [Required]
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the queue description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the default priority.
    /// </summary>
    public InteractionPriority DefaultPriority { get; set; } = InteractionPriority.Normal;

    /// <summary>
    /// Gets or sets the strategy used to choose which available agent receives the next queued item.
    /// </summary>
    public QueueRoutingStrategy RoutingStrategy { get; set; } = QueueRoutingStrategy.LongestIdle;

    /// <summary>
    /// Gets or sets a value indicating whether routing prefers the activity's last assigned user.
    /// </summary>
    public bool PreferStickyAgent { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether waiting items age in priority past the SLA threshold.
    /// </summary>
    public bool EnableSlaAging { get; set; }

    /// <summary>
    /// Gets or sets the SLA threshold in seconds.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int SlaThresholdSeconds { get; set; } = 120;

    /// <summary>
    /// Gets or sets the reservation timeout in seconds.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int ReservationTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the identifier of the business-hours calendar that gates when the queue routes work.
    /// </summary>
    public string BusinessHoursCalendarId { get; set; }

    /// <summary>
    /// Gets or sets the available business-hours calendars.
    /// </summary>
    public IList<SelectListItem> BusinessHoursCalendarOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the action taken for waiting items while the queue is closed.
    /// </summary>
    public QueueAfterHoursAction AfterHoursAction { get; set; } = QueueAfterHoursAction.HoldInQueue;

    /// <summary>
    /// Gets or sets the identifier of the queue that receives overflowed items.
    /// </summary>
    public string OverflowQueueId { get; set; }

    /// <summary>
    /// Gets or sets the available overflow queues.
    /// </summary>
    public IList<SelectListItem> OverflowQueueOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the seconds an item may wait before it overflows to the overflow queue.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int OverflowAfterSeconds { get; set; }

    /// <summary>
    /// Gets or sets the selected skills required to receive work from the queue.
    /// </summary>
    public IList<string> RequiredSkills { get; set; } = [];

    /// <summary>
    /// Gets or sets the available skills used by routing strategies.
    /// </summary>
    public IList<SelectListItem> SkillOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the inbound channel endpoint identifier mapped to this queue.
    /// </summary>
    public string InboundChannelEndpointId { get; set; }

    /// <summary>
    /// Gets or sets the available inbound channel endpoints.
    /// </summary>
    public IList<SelectListItem> InboundChannelEndpointOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the queue is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
