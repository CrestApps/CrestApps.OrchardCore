using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Users.Core;

/// <summary>
/// Represents the user permissions.
/// </summary>
public class UserPermissions
{
    public readonly static Permission ManageDisplaySettings = new("ManageDisplaySettings", "Manage the user display name settings.");

    public readonly static Permission ManageAvatarSettings = new("ManageAvatarSettings", "Manage the avatar settings.");
}
