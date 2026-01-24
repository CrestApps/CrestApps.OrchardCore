using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Services;

internal sealed class AIToolPermissionProvider : IPermissionProvider
{
    private readonly static IEnumerable<Permission> _allPermissions =
    [
        AIPermissions.ManageAIToolInstances,
        AIPermissions.AccessAnyAITool,
    ];

    private readonly ICatalog<AIToolInstance> _toolInstanceStore;
    private readonly AIToolDefinitionOptions _toolDefinitions;

    public AIToolPermissionProvider(
        ICatalog<AIToolInstance> toolInstanceStore,
        IOptions<AIToolDefinitionOptions> toolDefinitions)
    {
        _toolInstanceStore = toolInstanceStore;
        _toolDefinitions = toolDefinitions.Value;
    }

    public async Task<IEnumerable<Permission>> GetPermissionsAsync()
    {
        var permissions = new List<Permission>(_allPermissions);

        // Add dynamic permissions for tool definitions
        foreach (var toolName in _toolDefinitions.Tools.Keys)
        {
            permissions.Add(AIPermissions.CreateAIToolPermission(toolName));
        }

        // Add dynamic permissions for tool instances
        var instances = await _toolInstanceStore.GetAllAsync();
        foreach (var instance in instances)
        {
            permissions.Add(AIPermissions.CreateAIToolPermission(instance.ItemId));
        }

        return permissions;
    }

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new PermissionStereotype
        {
            Name = OrchardCoreConstants.Roles.Administrator,
            Permissions = _allPermissions,
        },
    ];
}
