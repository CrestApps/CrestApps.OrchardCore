using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Mcp.Services;

/// <summary>
/// Provides mcp server permissions functionality.
/// </summary>
public sealed class McpServerPermissionsProvider : IPermissionProvider
{
    public static readonly Permission AccessMcpServer = new("AccessMcpServer", "Access the MCP Server", isSecurityCritical: true);

    private readonly IEnumerable<Permission> _allPermissions =
    [
        AccessMcpServer,
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
