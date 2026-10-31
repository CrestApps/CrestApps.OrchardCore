namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// A worker who follows a rotation, and from when.
/// </summary>
public sealed class ShiftPatternWorker
{
    /// <summary>
    /// Gets or sets the worker.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets the day the worker is in week 0 of the rotation.
    /// </summary>
    public DateOnly StartDate { get; set; }
}
