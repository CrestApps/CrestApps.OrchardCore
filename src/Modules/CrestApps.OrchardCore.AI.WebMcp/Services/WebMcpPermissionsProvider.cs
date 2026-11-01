using OrchardCore;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.WebMcp.Services;

/// <summary>
/// Provides the permissions of the WebMCP module.
/// </summary>
public sealed class WebMcpPermissionsProvider : IPermissionProvider
{
    /// <summary>
    /// Lets a user give the site's WebMCP tools to AI agents in their browser. Each tool also needs the user's
    /// permission for that tool.
    /// </summary>
    public static readonly Permission UseWebMcpTools = new("UseWebMcpTools", LocalizationSource.Create<WebMcpPermissionsProvider>("Use WebMCP tools in the browser"), isSecurityCritical: true);

    /// <summary>
    /// Lets a user change the WebMCP settings.
    /// </summary>
    public static readonly Permission ManageWebMcpSettings = new("ManageWebMcpSettings", LocalizationSource.Create<WebMcpPermissionsProvider>("Manage the WebMCP settings"), isSecurityCritical: true);

    private readonly IEnumerable<Permission> _allPermissions =
    [
        UseWebMcpTools,
        ManageWebMcpSettings,
    ];

    /// <summary>
    /// Gets the permissions of the module.
    /// </summary>
    /// <returns>The permissions.</returns>
    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    /// <summary>
    /// Gets the roles that hold the permissions by default.
    /// </summary>
    /// <returns>The default stereotypes.</returns>
    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new PermissionStereotype
        {
            Name = OrchardCoreConstants.Roles.Administrator,
            Permissions = [UseWebMcpTools, ManageWebMcpSettings],
        },
    ];
}
