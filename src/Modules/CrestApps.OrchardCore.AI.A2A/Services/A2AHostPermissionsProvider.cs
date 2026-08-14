using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.A2A.Services;

/// <summary>
/// Provides a2 a host permissions functionality.
/// </summary>
public sealed class A2AHostPermissionsProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        A2APermissions.AccessA2AHost,
    ];

    /// <summary>
    /// Retrieves the permissions async.
    /// </summary>
    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    /// <summary>
    /// Retrieves the default stereotypes.
    /// </summary>
    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
        => [];
}
