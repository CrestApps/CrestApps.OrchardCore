using OrchardCore.Security.Permissions;

namespace CrestApps.Core.AI.A2A.Services;

/// <summary>
/// Provides a2 a host permissions functionality.
/// </summary>
public sealed class A2AHostPermissionsProvider : IPermissionProvider
{
    public static readonly Permission AccessA2AHost = new("AccessA2AHost", "Access the A2A Host", isSecurityCritical: true);

    private readonly IEnumerable<Permission> _allPermissions =
    [
        AccessA2AHost,
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
