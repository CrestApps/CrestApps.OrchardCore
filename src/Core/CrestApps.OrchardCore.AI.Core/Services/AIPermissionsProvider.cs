using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Core.Services;

internal sealed class AIPermissionsProvider : IPermissionProvider
{
    private readonly static IEnumerable<Permission> _allPermissions =
    [
        AIPermissions.ManageAIProfiles,
        AIPermissions.QueryAnyAIProfile,
    ];

    private readonly INamedModelStore<AIProfile> _profileStore;

    public AIPermissionsProvider(INamedModelStore<AIProfile> profileStore)
    {
        _profileStore = profileStore;
    }

    public async Task<IEnumerable<Permission>> GetPermissionsAsync()
    {
        var permissions = new List<Permission>(_allPermissions);

        foreach (var profile in await _profileStore.GetProfilesAsync(AIProfileType.Chat))
        {
            permissions.Add(AIPermissions.CreateProfilePermission(profile.Name));
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
