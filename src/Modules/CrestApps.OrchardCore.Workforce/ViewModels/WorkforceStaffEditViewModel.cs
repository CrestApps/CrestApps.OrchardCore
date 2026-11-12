namespace CrestApps.OrchardCore.Workforce.ViewModels;

/// <summary>
/// The values of Workforce's card on the staff editor.
/// </summary>
public class WorkforceStaffEditViewModel
{
    /// <summary>
    /// Gets or sets the fewest hours a week to schedule.
    /// </summary>
    public decimal? MinHoursPerWeek { get; set; }

    /// <summary>
    /// Gets or sets the most hours a week to schedule.
    /// </summary>
    public decimal? MaxHoursPerWeek { get; set; }

    /// <summary>
    /// Gets or sets the most hours a day to schedule.
    /// </summary>
    public decimal? MaxHoursPerDay { get; set; }

    /// <summary>
    /// Gets or sets the availability, one line per day.
    /// </summary>
    public string Availability { get; set; }
}
