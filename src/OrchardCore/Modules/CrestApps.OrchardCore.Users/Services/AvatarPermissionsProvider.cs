using CrestApps.OrchardCore.Users.Core;
using OrchardCore;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Users.Services;

[Feature(UsersConstants.Feature.Avatars)]
public sealed class AvatarPermissionsProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        UserPermissions.ManageAvatarSettings,
    ];

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new PermissionStereotype
        {
            Name = OrchardCoreConstants.Roles.Administrator,
            Permissions = _allPermissions
        },
    ];
}
