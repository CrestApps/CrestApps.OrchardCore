using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Users.Services;

/// <summary>
/// Declares the permission that guards the user search behind the user pickers.
/// </summary>
internal sealed class UserSearchPermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        UserSearchPermissions.SearchUsers,
    ];

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new PermissionStereotype
        {
            Name = OrchardCoreConstants.Roles.Administrator,
            Permissions = _allPermissions,
        },
    ];
}
