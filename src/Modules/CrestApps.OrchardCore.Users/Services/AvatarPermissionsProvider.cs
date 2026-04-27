using CrestApps.OrchardCore.Users.Core;
using OrchardCore;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Users.Services;

/// <summary>
/// Provides avatar permissions functionality.
/// </summary>
[Feature(UsersConstants.Feature.Avatars)]
public sealed class AvatarPermissionsProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        UserPermissions.ManageAvatarSettings,
    ];

    /// <summary>
    /// Retrieves the permissions async.
    /// </summary>
    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    /// <summary>
    /// Retrieves the default stereotypes.
    /// </summary>
    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new PermissionStereotype
        {
        Name = OrchardCoreConstants.Roles.Administrator,
        Permissions = _allPermissions
        },
    ];
}
