using CrestApps.OrchardCore.AI.Core;
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Services;

/// <summary>
/// Provides the permission used to manage AI personas.
/// </summary>
internal sealed class AIPersonaPermissionsProvider : IPermissionProvider
{
    private static readonly IEnumerable<Permission> _allPermissions =
    [
        AIPermissions.ManageAIPersonas,
    ];

    /// <summary>
    /// Retrieves the permissions.
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
            Permissions = _allPermissions,
        },
    ];
}
