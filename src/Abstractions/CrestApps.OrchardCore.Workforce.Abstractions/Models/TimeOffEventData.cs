namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// What Workforce stores on a Time off calendar event, to find the request it came from.
/// </summary>
public sealed class TimeOffEventData
{
    /// <summary>
    /// Gets or sets the request.
    /// </summary>
    public string RequestId { get; set; }

    /// <summary>
    /// Gets or sets the worker.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets the time-off type.
    /// </summary>
    public string TypeId { get; set; }
}
