using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Commerce.StoreProfile;

/// <summary>
/// The permissions of the Store Profile feature.
/// </summary>
public static class StoreProfilePermissions
{
    /// <summary>
    /// Edits the store's profile under Settings › Commerce › Store.
    /// </summary>
    public static readonly Permission ManageStoreProfile = new(
        "ManageStoreProfile",
        LocalizationSource.Create("Manage the store profile", typeof(StoreProfilePermissions)));
}
