using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Scheduling.ViewModels;

/// <summary>
/// The editor of a scheduling resource.
/// </summary>
public class SchedulingResourceViewModel
{
    /// <summary>Gets or sets the id.</summary>
    public string Id { get; set; }

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; }

    /// <summary>Gets or sets the kind.</summary>
    public string Kind { get; set; }

    /// <summary>Gets or sets the staff member's user id.</summary>
    public string UserId { get; set; }

    /// <summary>Gets or sets the calendar, or empty to create one.</summary>
    public string CalendarId { get; set; }

    /// <summary>Gets or sets the time zone.</summary>
    public string TimeZoneId { get; set; }

    /// <summary>Gets or sets where the hours come from.</summary>
    public string AvailabilityMode { get; set; }

    /// <summary>Gets or sets the workforce calendars for shifts.</summary>
    public string[] ShiftCalendarIds { get; set; } = [];

    /// <summary>Gets or sets the weekly hours, one line per day, such as Monday 09:00-12:00, 13:00-17:00.</summary>
    public string WorkingHours { get; set; }

    /// <summary>Gets or sets the date overrides, one per line, such as 2026-12-24 closed or 2026-12-31 09:00-12:00.</summary>
    public string DateOverrides { get; set; }

    /// <summary>Gets or sets whether it takes appointments.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the selected user, as the user picker reads it.</summary>
    [BindNever]
    public string InitialUserJson { get; set; } = "[]";

    /// <summary>Gets or sets the calendars to choose from.</summary>
    [BindNever]
    public IList<SelectListItem> CalendarOptions { get; set; } = [];

    /// <summary>Gets or sets the calendars to choose shift calendars from.</summary>
    [BindNever]
    public IList<SelectListItem> ShiftCalendarOptions { get; set; } = [];

    /// <summary>Gets or sets the kinds to choose from.</summary>
    [BindNever]
    public IList<SelectListItem> KindOptions { get; set; } = [];

    /// <summary>Gets or sets the time zones to choose from.</summary>
    [BindNever]
    public IList<SelectListItem> TimeZoneOptions { get; set; } = [];
}
