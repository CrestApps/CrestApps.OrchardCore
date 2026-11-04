using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.ViewModels;

/// <summary>
/// One row of the rotation list.
/// </summary>
public class RotationRowViewModel
{
    /// <summary>
    /// Gets or sets the rotation.
    /// </summary>
    public ShiftPattern Pattern { get; set; }

    /// <summary>
    /// Gets or sets the name of its team.
    /// </summary>
    public string TeamName { get; set; }
}
