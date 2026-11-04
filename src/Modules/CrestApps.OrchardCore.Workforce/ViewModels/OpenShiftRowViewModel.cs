using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.ViewModels;

/// <summary>
/// One row of the open shift list.
/// </summary>
public class OpenShiftRowViewModel
{
    /// <summary>
    /// Gets or sets the open shift.
    /// </summary>
    public OpenShift Shift { get; set; }

    /// <summary>
    /// Gets or sets the name of its team.
    /// </summary>
    public string TeamName { get; set; }

    /// <summary>
    /// Gets or sets the name of the worker who got it.
    /// </summary>
    public string FilledWorkerName { get; set; }
}
