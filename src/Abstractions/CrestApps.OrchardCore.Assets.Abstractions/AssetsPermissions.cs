using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Assets;

/// <summary>
/// The permissions of the Assets module.
/// </summary>
public static class AssetsPermissions
{
    /// <summary>
    /// Allows managing asset types, assets and their readings.
    /// </summary>
    public static readonly Permission ManageAssets = new("ManageAssets", LocalizationSource.Create("Manage assets", typeof(AssetsPermissions)));

    /// <summary>
    /// Allows seeing assets and their history.
    /// </summary>
    public static readonly Permission ViewAssets = new("ViewAssets", LocalizationSource.Create("View assets", typeof(AssetsPermissions)), [ManageAssets]);
}
