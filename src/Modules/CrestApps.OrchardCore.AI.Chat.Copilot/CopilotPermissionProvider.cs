using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Chat.Copilot;

public sealed class CopilotPermissionProvider : IPermissionProvider
{
    public static readonly Permission ManageCopilotSettings = new("ManageCopilotSettings", "Manage Copilot Settings");

    private readonly IEnumerable<Permission> _allPermissions =
    [
        ManageCopilotSettings,
    ];

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new PermissionStereotype
        {
            Name = OrchardCoreConstants.Roles.Administrator,
            Permissions = _allPermissions,
        },
    ];
}
