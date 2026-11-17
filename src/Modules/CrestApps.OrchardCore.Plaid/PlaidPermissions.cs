using OrchardCore;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Plaid;

/// <summary>
/// The Plaid permissions.
/// </summary>
public static class PlaidPermissions
{
    /// <summary>
    /// Change the Plaid settings: the environment, the client id and which secret holds the Plaid secret.
    /// </summary>
    public static readonly Permission ManagePlaidSettings = new("ManagePlaidSettings", LocalizationSource.Create("Manage Plaid settings", typeof(PlaidPermissions)), isSecurityCritical: true);
}

/// <summary>
/// Declares the Plaid permissions.
/// </summary>
internal sealed class PlaidPermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        PlaidPermissions.ManagePlaidSettings,
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
