using CrestApps.OrchardCore.AI.Azure.Core;
using CrestApps.OrchardCore.AI.Models;
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class AIChatPermissionsProvider : IPermissionProvider
{
    private readonly static IEnumerable<Permission> _allPermissions =
    [
        AIChatPermissions.ManageAIChatProfiles,
        AIChatPermissions.QueryAnyAIChatProfile,
    ];

    private readonly IAIChatProfileStore _chatProfileStore;

    public AIChatPermissionsProvider(IAIChatProfileStore chatProfileStore)
    {
        _chatProfileStore = chatProfileStore;
    }

    public async Task<IEnumerable<Permission>> GetPermissionsAsync()
    {
        var permissions = new List<Permission>(_allPermissions);

        foreach (var profile in await _chatProfileStore.GetProfilesAsync(AIChatProfileType.Chat))
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
