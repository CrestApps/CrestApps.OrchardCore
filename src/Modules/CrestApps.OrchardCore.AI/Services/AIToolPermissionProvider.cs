using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Services;

internal sealed class AIToolPermissionProvider : IPermissionProvider
{
    private readonly static IEnumerable<Permission> _allPermissions =
    [
        AIPermissions.AccessAnyAITool,
    ];

    private readonly AIToolDefinitionOptions _toolDefinitions;

    public AIToolPermissionProvider(
        IOptions<AIToolDefinitionOptions> toolDefinitions)
    {
        _toolDefinitions = toolDefinitions.Value;
    }

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
    {
        var permissions = new List<Permission>(_allPermissions);

        // Add dynamic permissions for tool definitions
        foreach (var toolName in _toolDefinitions.Tools.Keys)
        {
            permissions.Add(AIPermissions.CreateAIToolPermission(toolName));
        }

        return Task.FromResult<IEnumerable<Permission>>(permissions);
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
