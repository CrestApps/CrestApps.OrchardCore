using CrestApps.OrchardCore.OpenAI.Azure.Core;
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.OpenAI.Core.Services;

public sealed class OpenAIDeploymentProvider : IPermissionProvider
{
    private readonly static IEnumerable<Permission> _allPermissions =
    [
        OpenAIChatPermissions.ManageModelDeployments,
    ];

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
    {
        return
        [
            new PermissionStereotype
            {
                Name = OrchardCoreConstants.Roles.Administrator,
                Permissions = _allPermissions,
            },
        ];
    }
}

