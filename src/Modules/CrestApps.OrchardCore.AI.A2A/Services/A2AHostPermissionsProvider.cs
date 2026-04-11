using OrchardCore.Security.Permissions;

namespace CrestApps.Core.AI.A2A.Services;

public sealed class A2AHostPermissionsProvider : IPermissionProvider
{
    public static readonly Permission AccessA2AHost = new("AccessA2AHost", "Access the A2A Host", isSecurityCritical: true);

    private readonly IEnumerable<Permission> _allPermissions =
    [
        AccessA2AHost,
    ];

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
        => [];
}
