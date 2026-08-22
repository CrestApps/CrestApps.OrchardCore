using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Products.Services;

/// <summary>
/// Exposes the permissions used by the currency-management screens.
/// </summary>
internal sealed class CurrencyPermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _permissions =
    [
        ProductsConstants.Permissions.ManageCurrencies,
    ];

    /// <summary>
    /// Retrieves the default stereotypes.
    /// </summary>
    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
        =>
    [
        new PermissionStereotype
        {
            Name = OrchardCoreConstants.Roles.Administrator,
            Permissions = _permissions,
        },
    ];

    /// <summary>
    /// Retrieves the permissions asynchronously.
    /// </summary>
    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_permissions);
}
