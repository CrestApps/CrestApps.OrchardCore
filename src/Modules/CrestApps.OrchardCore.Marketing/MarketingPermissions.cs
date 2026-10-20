using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Marketing;

public static class MarketingPermissions
{
    /// <summary>
    /// Adds and changes segments and campaigns, and sends campaigns.
    /// </summary>
    public static readonly Permission ManageMarketing = new("ManageMarketing", "Manage marketing");
}

internal sealed class MarketingPermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        MarketingPermissions.ManageMarketing,
    ];

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
        =>
        [
            new PermissionStereotype
            {
                Name = OrchardCoreConstants.Roles.Administrator,
                Permissions = _allPermissions,
            },
        ];
}
