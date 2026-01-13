using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.McpServer.Services;

public sealed class McpServerPermissionsProvider : IPermissionProvider
{
    public static readonly Permission AccessMcpServer = new("AccessMcpServer", "Access the MCP Server", isSecurityCritical: true);

    private readonly IEnumerable<Permission> _allPermissions =
    [
        AccessMcpServer,
    ];

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
        => [];
}
