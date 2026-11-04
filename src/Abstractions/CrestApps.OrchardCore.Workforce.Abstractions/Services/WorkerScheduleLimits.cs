namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// The limits a worker's shifts must keep to: from their labor rule set, or else from the shift planning options and the worker's own hours.
/// </summary>
public sealed class WorkerScheduleLimits
{
    /// <summary>
    /// Gets or sets the fewest hours between shifts.
    /// </summary>
    public decimal MinRestHours { get; set; }

    /// <summary>
    /// Gets or sets the most days in a row.
    /// </summary>
    public int MaxConsecutiveDays { get; set; }

    /// <summary>
    /// Gets or sets the most hours a week; empty for no limit.
    /// </summary>
    public decimal? MaxHoursPerWeek { get; set; }

    /// <summary>
    /// Gets or sets the most hours a day; empty for no limit.
    /// </summary>
    public decimal? MaxHoursPerDay { get; set; }

    /// <summary>
    /// Gets or sets whether a labor rule set gave the limits, in which case its rules check them.
    /// </summary>
    public bool FromRuleSet { get; set; }
}
