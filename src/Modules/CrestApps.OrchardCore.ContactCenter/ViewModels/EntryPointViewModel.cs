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
    /// Gets or sets the target queue identifier.
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
