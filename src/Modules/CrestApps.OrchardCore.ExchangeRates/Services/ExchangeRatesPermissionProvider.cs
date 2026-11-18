using CrestApps.OrchardCore.ExchangeRates.Core;
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ExchangeRates.Services;

/// <summary>
/// Declares the exchange rate permission, given to administrators.
/// </summary>
internal sealed class ExchangeRatesPermissionProvider : IPermissionProvider
{
    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult<IEnumerable<Permission>>(ExchangeRatesPermissions.All);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
        =>
        [
            new PermissionStereotype
            {
                Name = OrchardCoreConstants.Roles.Administrator,
                Permissions = ExchangeRatesPermissions.All,
            },
        ];
}
