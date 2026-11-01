namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// What a worker asks for.
/// </summary>
public sealed class TimeOffRequestInput
{
    /// <summary>
    /// Gets or sets the worker.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets the time-off type.
    /// </summary>
    public string TypeId { get; set; }

    /// <summary>
    /// Gets or sets the first day.
    /// </summary>
    public DateOnly StartDate { get; set; }

    /// <summary>
    /// Gets or sets the last day.
    /// </summary>
    public DateOnly EndDate { get; set; }

    /// <summary>
    /// Gets or sets when the time off starts, for part of one day.
    /// </summary>
    public TimeOnly? StartTime { get; set; }

    /// <summary>
    /// Gets or sets when the time off ends, for part of one day.
    /// </summary>
    public TimeOnly? EndTime { get; set; }

    /// <summary>
    /// Gets or sets the reason.
    /// </summary>
    public string Reason { get; set; }
}
