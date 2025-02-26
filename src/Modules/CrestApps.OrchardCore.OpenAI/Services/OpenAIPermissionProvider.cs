using CrestApps.OrchardCore.AI.Core;
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.OpenAI.Services;

internal sealed class OpenAIPermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        AIPermissions.ManageOpenAIConnections,
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
