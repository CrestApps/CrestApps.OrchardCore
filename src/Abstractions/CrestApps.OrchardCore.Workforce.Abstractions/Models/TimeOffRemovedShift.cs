namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// A published shift that approved time off took off the schedule.
/// </summary>
public sealed class TimeOffRemovedShift
{
    /// <summary>
    /// Gets or sets the shift's calendar event, canceled.
    /// </summary>
    public string EventId { get; set; }

    /// <summary>
    /// Gets or sets the shift's draft as it was, or <see langword="null"/> when it had none.
    /// </summary>
    public DraftShift Draft { get; set; }
}
