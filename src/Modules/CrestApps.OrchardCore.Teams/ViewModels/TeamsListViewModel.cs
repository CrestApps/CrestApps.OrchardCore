using CrestApps.OrchardCore.Teams.Models;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Teams.ViewModels;

/// <summary>
/// The team list.
/// </summary>
public class TeamsListViewModel
{
    /// <summary>
    /// Gets or sets the text the names must contain.
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// Gets or sets the teams of the page, for the admin list.
    /// </summary>
    public AdminListContext<Team> List { get; set; }
}
