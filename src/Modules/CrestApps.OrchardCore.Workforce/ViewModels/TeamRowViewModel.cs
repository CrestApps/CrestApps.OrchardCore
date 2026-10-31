using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.ViewModels;

/// <summary>
/// A row of the team list.
/// </summary>
public class TeamRowViewModel
{
    /// <summary>
    /// Gets or sets the team.
    /// </summary>
    public WorkforceTeam Team { get; set; }

    /// <summary>
    /// Gets or sets the name of the team it belongs to.
    /// </summary>
    public string ParentName { get; set; }
}
