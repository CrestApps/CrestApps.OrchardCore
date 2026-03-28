using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.Logging;
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

    private readonly IAIProfileStore _profileStore;
    private readonly ILogger _logger;

    public AIPermissionsProvider(
        IAIProfileStore profileStore,
        ILogger<AIPermissionsProvider> logger)
    {
        _profileStore = profileStore;
        _logger = logger;
    }

    public async Task<IEnumerable<Permission>> GetPermissionsAsync()
    {
        var permissions = new List<Permission>(_allPermissions);

        try
        {
            foreach (var profile in await _profileStore.GetAllAsync())
            {
                permissions.Add(AIPermissions.CreateProfilePermission(profile.Name));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while retrieving AI profiles to generate permissions.");
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
