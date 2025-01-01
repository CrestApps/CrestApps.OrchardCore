using CrestApps.OrchardCore.OpenAI.Azure.Core;
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.OpenAI.Core.Services;

public sealed class OpenAIChatPermissionsProvider : IPermissionProvider
{
    private readonly static IEnumerable<Permission> _allPermissions =
    [
        OpenAIChatPermissions.ManageAIChatProfiles,
        OpenAIChatPermissions.QueryAnyAIChatProfile,
    ];

    private readonly IOpenAIChatProfileStore _aIChatProfileStore;

    public OpenAIChatPermissionsProvider(IOpenAIChatProfileStore aIChatProfileStore)
    {
        _aIChatProfileStore = aIChatProfileStore;
    }

    public async Task<IEnumerable<Permission>> GetPermissionsAsync()
    {
        var permissions = new List<Permission>()
        {
            OpenAIChatPermissions.ManageAIChatProfiles,
            OpenAIChatPermissions.QueryAnyAIChatProfile,
        };

        foreach (var profile in await _aIChatProfileStore.GetAllAsync())
        {
            permissions.Add(OpenAIChatPermissions.CreateDynamicPermission(profile.Name));
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

