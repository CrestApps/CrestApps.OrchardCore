using CrestApps.OrchardCore.Users.Core;
using OrchardCore;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Users.Services;

/// <summary>
/// Provides user display name permissions functionality.
/// </summary>
[Feature(UsersConstants.Feature.DisplayName)]
public sealed class UserDisplayNamePermissionsProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        UserPermissions.ManageDisplaySettings,
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
