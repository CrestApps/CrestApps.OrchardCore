using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Users.Core;

/// <summary>
/// Represents the user permissions.
/// </summary>
public class UserPermissions
{
    public readonly static Permission ManageDisplaySettings = new("ManageDisplaySettings", LocalizationSource.Create<UserPermissions>("Manage the user display name settings."));

    public readonly static Permission ManageAvatarSettings = new("ManageAvatarSettings", LocalizationSource.Create<UserPermissions>("Manage the avatar settings."));
}
