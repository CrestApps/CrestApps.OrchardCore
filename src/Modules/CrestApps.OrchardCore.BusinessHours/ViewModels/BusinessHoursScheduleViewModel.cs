using CrestApps.OrchardCore.BusinessHours.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.BusinessHours.ViewModels;

/// <summary>
/// The editor of a business hours schedule. Its cards (general, weekly hours, special days) edit the same model under
/// the same prefix, so the form posts them together.
/// </summary>
public class BusinessHoursScheduleViewModel
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the time zone the hours are in.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets whether the schedule is in use.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the days of the week, from Monday.
    /// </summary>
    public IList<WeeklyHoursRow> Days { get; set; } = [];

    /// <summary>
    /// Gets or sets the holidays and special days, one per line.
    /// </summary>
    public string SpecialDays { get; set; }

    /// <summary>
    /// Gets or sets the time zones to pick from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> TimeZoneOptions { get; set; } = [];
}
