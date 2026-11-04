using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Teams;

/// <summary>
/// The Teams permissions.
/// </summary>
public static class TeamsPermissions
{
    /// <summary>
    /// Add, change and remove teams.
    /// </summary>
    public static readonly Permission ManageTeams = new("ManageTeams", LocalizationSource.Create("Manage teams", typeof(TeamsPermissions)));
}
