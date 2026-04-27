using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Chat.Copilot;

/// <summary>
/// Provides copilot permission functionality.
/// </summary>
public sealed class CopilotPermissionProvider : IPermissionProvider
{
    public static readonly Permission ManageCopilotSettings = new("ManageCopilotSettings", "Manage Copilot Settings");

    private readonly IEnumerable<Permission> _allPermissions =
    [
        ManageCopilotSettings,
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
        Permissions = _allPermissions,
        },
    ];
}
