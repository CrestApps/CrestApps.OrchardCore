using CrestApps.Core.AI.Tooling;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Services;

/// <summary>
/// Provides the permissions used to manage AI tool instances, along with a dynamic access permission for
/// every configured instance so access can be granted to the AI model on a per-role basis.
/// </summary>
internal sealed class AIToolInstancePermissionsProvider : IPermissionProvider
{
    private static readonly IEnumerable<Permission> _allPermissions =
    [
        AIPermissions.ManageAIToolInstances,
        AIPermissions.ManageAIToolInstancesCreatedByOthers,
    ];

    private readonly ISourceCatalog<AIToolInstance> _instancesCatalog;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolInstancePermissionsProvider"/> class.
    /// </summary>
    /// <param name="instancesCatalog">The tool instances catalog.</param>
    /// <param name="logger">The logger.</param>
    public AIToolInstancePermissionsProvider(
        ISourceCatalog<AIToolInstance> instancesCatalog,
        ILogger<AIToolInstancePermissionsProvider> logger)
    {
        _instancesCatalog = instancesCatalog;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves the permissions, including a dynamic access permission for every configured instance so
    /// each instance can be granted to the AI model on a per-role basis.
    /// </summary>
    public async Task<IEnumerable<Permission>> GetPermissionsAsync()
    {
        var permissions = new List<Permission>(_allPermissions);

        try
        {
            foreach (var instance in await _instancesCatalog.GetAllAsync())
            {
                permissions.Add(AIPermissions.CreateAIToolPermission(instance.GetFunctionName()));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while retrieving AI tool instances to generate permissions.");
        }

        return permissions;
    }

    /// <summary>
    /// Retrieves the default stereotypes.
    /// </summary>
    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new PermissionStereotype
        {
            Name = OrchardCoreConstants.Roles.Administrator,
            Permissions = _allPermissions,
        },
    ];
}
