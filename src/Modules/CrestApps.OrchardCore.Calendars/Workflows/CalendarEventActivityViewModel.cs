using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Calendars.Workflows;

/// <summary>
/// The editor of a calendar workflow event.
/// </summary>
public class CalendarEventActivityViewModel
{
    /// <summary>
    /// Gets or sets the calendars that start the workflow.
    /// </summary>
    public string[] CalendarIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the calendar types that start the workflow.
    /// </summary>
    public string[] CalendarTypes { get; set; } = [];

    /// <summary>
    /// Gets or sets the event types that start the workflow, comma separated.
    /// </summary>
    public string EventTypes { get; set; }

    /// <summary>
    /// Gets or sets whether changes made by workflows are ignored.
    /// </summary>
    public bool IgnoreWorkflowChanges { get; set; } = true;

    /// <summary>
    /// Gets or sets how many minutes before the start, for the Starting Soon event.
    /// </summary>
    public int MinutesBefore { get; set; } = 15;

    /// <summary>
    /// Gets or sets whether the event has a minutes-before setting.
    /// </summary>
    [BindNever]
    public bool HasMinutesBefore { get; set; }

    /// <summary>
    /// Gets or sets the calendars to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> CalendarOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the calendar types to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> TypeOptions { get; set; } = [];
}
