using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Users;

/// <summary>
/// The permissions that guard the user search behind the user pickers.
/// </summary>
public sealed class UserSearchPermissions
{
    // Orchard Core's admin panel permission, by name: a permission is granted by its name, so this project needs no
    // reference to the Admin module.
    private static readonly Permission _accessAdminPanel = new("AccessAdminPanel");

    /// <summary>
    /// Search the users of the site from a user picker. It is implied by the admin panel permission, so every role that
    /// may open the admin panel keeps its pickers, while a signed-in storefront customer cannot list the site's users.
    /// </summary>
    public static readonly Permission SearchUsers = new(
        "SearchUsers",
        LocalizationSource.Create<UserSearchPermissions>("Search users in user pickers"),
        [_accessAdminPanel]);

    private UserSearchPermissions()
    {
    }
}
