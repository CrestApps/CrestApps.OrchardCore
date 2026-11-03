namespace CrestApps.OrchardCore.Scheduling.Models;

/// <summary>
/// Where a resource's available hours come from.
/// </summary>
public enum SchedulingAvailabilityMode
{
    /// <summary>
    /// Its weekly working hours, less its date overrides.
    /// </summary>
    WorkingHours,

    /// <summary>
    /// The shifts it works on a workforce calendar.
    /// </summary>
    Shifts,
}
