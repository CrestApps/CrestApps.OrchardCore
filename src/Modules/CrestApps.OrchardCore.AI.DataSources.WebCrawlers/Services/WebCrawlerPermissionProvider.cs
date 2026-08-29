using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.DataSources.WebCrawlers.Services;

internal sealed class WebCrawlerPermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        WebCrawlerPermissions.ManageWebCrawlers,
    ];

    /// <summary>
    /// Retrieves the permissions async.
    /// </summary>
    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

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
