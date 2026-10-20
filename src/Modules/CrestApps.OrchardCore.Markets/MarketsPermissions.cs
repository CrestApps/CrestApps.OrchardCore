using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Markets;

public static class MarketsPermissions
{
    /// <summary>
    /// Adds and changes markets: their countries, currency and exchange rate, rounding, language and tax.
    /// </summary>
    public static readonly Permission ManageMarkets = new("ManageMarkets", "Manage markets");
}

internal sealed class MarketsPermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        MarketsPermissions.ManageMarkets,
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
