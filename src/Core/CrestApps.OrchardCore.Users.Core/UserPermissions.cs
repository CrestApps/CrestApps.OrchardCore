using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Users.Core;

/// <summary>
/// Represents the user permissions.
/// </summary>
public static class UserPermissions
{
    public readonly static Permission ManageDisplaySettings = new("ManageDisplaySettings", LocalizationSource.Create("Manage the user display name settings.", typeof(UserPermissions)));

    public readonly static Permission ManageAvatarSettings = new("ManageAvatarSettings", LocalizationSource.Create("Manage the avatar settings.", typeof(UserPermissions)));
}
