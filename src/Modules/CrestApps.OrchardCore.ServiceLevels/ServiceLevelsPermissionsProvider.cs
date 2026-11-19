using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ServiceLevels;

/// <summary>
/// Provides the Service Levels permissions.
/// </summary>
internal sealed class ServiceLevelsPermissionsProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        ServiceLevelsPermissions.ManageServiceLevelPolicies,
    ];

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
    {
        return
        [
            new PermissionStereotype
            {
                Name = OrchardCoreConstants.Roles.Administrator,
                Permissions = _allPermissions,
            },
        ];
    }
}
