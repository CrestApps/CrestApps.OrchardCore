using CrestApps.OrchardCore.AI.Models;
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class AIPermissionsProvider : IPermissionProvider
{
    private readonly static IEnumerable<Permission> _allPermissions =
    [
        AIPermissions.ManageAIProfiles,
        AIPermissions.QueryAnyAIProfile,
    ];

    private readonly IAIProfileStore _profileStore;

    public AIPermissionsProvider(IAIProfileStore profileStore)
    {
        _profileStore = profileStore;
    }

    public async Task<IEnumerable<Permission>> GetPermissionsAsync()
    {
        var permissions = new List<Permission>(_allPermissions);

        foreach (var profile in await _profileStore.GetProfilesAsync(AIProfileType.Chat))
        {
            permissions.Add(AIPermissions.CreateDynamicPermission(profile.Name));
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
