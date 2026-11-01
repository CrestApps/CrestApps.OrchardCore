using CrestApps.OrchardCore.Workforce.Models;
namespace CrestApps.OrchardCore.Workforce.ViewModels;

/// <summary>
/// A row of the time-off queue.
/// </summary>
public class TimeOffRequestRowViewModel
{
    /// <summary>
    /// Gets or sets the request.
    /// </summary>
    public TimeOffRequest Request { get; set; }

    /// <summary>
    /// Gets or sets the worker's name.
    /// </summary>
    public string WorkerName { get; set; }

    /// <summary>
    /// Gets or sets the team's name.
    /// </summary>
    public string TeamName { get; set; }

    /// <summary>
    /// Gets or sets the time-off type.
    /// </summary>
    public TimeOffType Type { get; set; }
}
