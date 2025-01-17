using CrestApps.OrchardCore.OpenAI.Azure.Core;
using CrestApps.OrchardCore.OpenAI.Models;
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

    private readonly IOpenAIChatProfileStore _chatProfileStore;

    public OpenAIChatPermissionsProvider(IOpenAIChatProfileStore chatProfileStore)
    {
        _chatProfileStore = chatProfileStore;
    }

    public async Task<IEnumerable<Permission>> GetPermissionsAsync()
    {
        var permissions = new List<Permission>(_allPermissions);

        foreach (var profile in await _chatProfileStore.GetProfilesAsync(OpenAIChatProfileType.Chat))
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
