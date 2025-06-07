using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Services;

internal sealed class AIPermissionsProvider : IPermissionProvider
{
    private readonly static IEnumerable<Permission> _allPermissions =
    [
        AIPermissions.ManageAIProfiles,
        AIPermissions.QueryAnyAIProfile,
    ];

    private readonly INamedCatalog<AIProfile> _profilesCatalog;

    public AIPermissionsProvider(INamedCatalog<AIProfile> profilesCatalog)
    {
        _profilesCatalog = profilesCatalog;
    }

    public async Task<IEnumerable<Permission>> GetPermissionsAsync()
    {
        var permissions = new List<Permission>(_allPermissions);

        foreach (var profile in await _profilesCatalog.GetProfilesAsync(AIProfileType.Chat))
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
