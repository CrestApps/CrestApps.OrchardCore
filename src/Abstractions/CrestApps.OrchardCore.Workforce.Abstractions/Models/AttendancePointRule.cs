namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// The points an attendance exception earns.
/// </summary>
public sealed class AttendancePointRule
{
    /// <summary>
    /// Gets or sets the kind of exception.
    /// </summary>
    public AttendanceExceptionKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the points.
    /// </summary>
    public decimal Points { get; set; }
}
