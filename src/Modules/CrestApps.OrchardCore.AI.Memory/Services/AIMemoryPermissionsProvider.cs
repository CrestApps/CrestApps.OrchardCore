using CrestApps.OrchardCore.AI.Core;
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Memory.Services;

internal sealed class AIMemoryPermissionsProvider : IPermissionProvider
{
    private static readonly IEnumerable<Permission> _allPermissions =
    [
        AIPermissions.ClearAIMemoryForOthers,
        AIPermissions.ClearAIMemory,
    ];

    /// <summary>
    /// Retrieves the permissions.
    /// </summary>
    public Task<IEnumerable<Permission>> GetPermissionsAsync()
    {
        return Task.FromResult(_allPermissions);
    }

    /// <summary>
    /// Retrieves the default stereotypes.
    /// </summary>
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
