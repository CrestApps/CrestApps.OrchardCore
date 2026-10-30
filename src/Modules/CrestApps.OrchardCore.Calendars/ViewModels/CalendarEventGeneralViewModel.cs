using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Calendars.ViewModels;

/// <summary>
/// The General card of the event editor.
/// </summary>
public class CalendarEventGeneralViewModel
{
    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the event type.
    /// </summary>
    public string EventType { get; set; }

    /// <summary>
    /// Gets or sets whether the event lasts whole days.
    /// </summary>
    public bool AllDay { get; set; }

    /// <summary>
    /// Gets or sets the start date, as yyyy-MM-dd.
    /// </summary>
    public string StartDate { get; set; }

    /// <summary>
    /// Gets or sets the start time, as HH:mm.
    /// </summary>
    public string StartTime { get; set; }

    /// <summary>
    /// Gets or sets the end date, as yyyy-MM-dd; for an all-day event, the last day.
    /// </summary>
    public string EndDate { get; set; }

    /// <summary>
    /// Gets or sets the end time, as HH:mm.
    /// </summary>
    public string EndTime { get; set; }

    /// <summary>
    /// Gets or sets the time zone.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets where the event happens.
    /// </summary>
    public string Location { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets whether the event takes time.
    /// </summary>
    public string Transparency { get; set; }

    /// <summary>
    /// Gets or sets how many minutes before the start the event reminds its people, or empty for no reminder.
    /// </summary>
    public string ReminderMinutes { get; set; }

    /// <summary>
    /// Gets or sets whether reminders are on, so the reminder can be chosen.
    /// </summary>
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public bool RemindersEnabled { get; set; }

    /// <summary>
    /// Gets or sets the reminders to choose from.
    /// </summary>
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public IList<SelectListItem> ReminderOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets who sees the details.
    /// </summary>
    public string Visibility { get; set; }

    /// <summary>
    /// Gets or sets a color that overrides the type's and the calendar's.
    /// </summary>
    public string Color { get; set; }

    /// <summary>
    /// Gets or sets whether a color overrides the type's and the calendar's.
    /// </summary>
    public bool UseColor { get; set; }

    /// <summary>
    /// Gets or sets the calendar's name.
    /// </summary>
    [BindNever]
    public string CalendarName { get; set; }

    /// <summary>
    /// Gets or sets whether the calendar supports all-day events.
    /// </summary>
    [BindNever]
    public bool SupportsAllDay { get; set; }

    /// <summary>
    /// Gets or sets the event types to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> EventTypeOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the time zones to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> TimeZoneOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the statuses to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> StatusOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the busy/free options.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> TransparencyOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the visibilities to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> VisibilityOptions { get; set; } = [];
}
