using System.ComponentModel.DataAnnotations;

namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// Represents the edit view model for a business-hours calendar.
/// </summary>
public class BusinessHoursCalendarViewModel
{
    /// <summary>
    /// Gets or sets the calendar identifier.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the unique calendar name.
    /// </summary>
    [Required]
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the calendar description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the time zone the schedule is evaluated in.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets the per-day open windows.
    /// </summary>
    public IList<BusinessHoursDayViewModel> Days { get; set; } = [];

    /// <summary>
    /// Gets or sets the holiday dates, one ISO date (yyyy-MM-dd) per line.
    /// </summary>
    public string HolidaysText { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the calendar is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
