using OrchardCore;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Mcp.Services;

/// <summary>
/// Provides mcp server permissions functionality.
/// </summary>
public sealed class McpServerPermissionsProvider : IPermissionProvider
{
    public static readonly Permission AccessMcpServer = new("AccessMcpServer", LocalizationSource.Create<McpServerPermissionsProvider>("Access the MCP Server"), isSecurityCritical: true);

    public static readonly Permission ManageMcpServerSettings = new("ManageMcpServerSettings", LocalizationSource.Create<McpServerPermissionsProvider>("Manage the MCP Server settings"), isSecurityCritical: true);

    private readonly IEnumerable<Permission> _allPermissions =
    [
        AccessMcpServer,
        ManageMcpServerSettings,
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
            Permissions = [ManageMcpServerSettings],
        },
    ];
}
