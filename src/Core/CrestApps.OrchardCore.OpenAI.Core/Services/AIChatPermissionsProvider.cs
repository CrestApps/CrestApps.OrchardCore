using CrestApps.OrchardCore.OpenAI.Azure.Core;
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.OpenAI.Core.Services;

public sealed class AIChatPermissionsProvider : IPermissionProvider
{
    private readonly static IEnumerable<Permission> _allPermissions =
    [
        AIChatPermissions.ManageAIChatProfiles
    ];

    private readonly IAIChatProfileStore _aIChatProfileStore;

    public AIChatPermissionsProvider(IAIChatProfileStore aIChatProfileStore)
    {
        _aIChatProfileStore = aIChatProfileStore;
    }

    public async Task<IEnumerable<Permission>> GetPermissionsAsync()
    {
        var permissions = new List<Permission>()
        {
            AIChatPermissions.ManageAIChatProfiles,
        };

        foreach (var profile in await _aIChatProfileStore.GetAllAsync())
        {
            permissions.Add(AIChatPermissions.CreateDynamicPermission(profile.Name));
        }

        return permissions;
    }

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

