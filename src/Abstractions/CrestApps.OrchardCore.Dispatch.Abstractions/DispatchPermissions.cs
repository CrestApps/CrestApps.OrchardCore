using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Dispatch;

/// <summary>
/// The permissions of the Dispatch module.
/// </summary>
public static class DispatchPermissions
{
    /// <summary>
    /// Change the dispatch settings.
    /// </summary>
    public static readonly Permission ManageDispatchSettings = new("ManageDispatchSettings", LocalizationSource.Create("Manage dispatch settings", typeof(DispatchPermissions)));

    /// <summary>
    /// Assign, move and change visits.
    /// </summary>
    public static readonly Permission ManageDispatch = new("ManageDispatch", LocalizationSource.Create("Dispatch visits", typeof(DispatchPermissions)), [ManageDispatchSettings]);

    /// <summary>
    /// See the visits and the board.
    /// </summary>
    public static readonly Permission ViewDispatch = new("ViewDispatch", LocalizationSource.Create("View visits and the dispatch board", typeof(DispatchPermissions)), [ManageDispatch]);
}
