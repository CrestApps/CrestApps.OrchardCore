using CrestApps.OrchardCore.Stripe.Core;
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Stripe.Services;

/// <summary>
/// Provides Stripe module permissions and their default role stereotypes.
/// </summary>
public sealed class StripePermissionsProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        StripePermissions.ManageStripeSettings,
    ];

    /// <summary>
    /// Gets all permissions exposed by the Stripe module.
    /// </summary>
    /// <returns>The collection of Stripe permissions.</returns>
    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    /// <summary>
    /// Gets the default role stereotypes for Stripe permissions.
    /// </summary>
    /// <returns>The default permission stereotypes.</returns>
    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new PermissionStereotype
        {
            Name = OrchardCoreConstants.Roles.Administrator,
            Permissions = _allPermissions,
        },
    ];
}
