using CrestApps.OrchardCore.Subscriptions.Core;
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Subscriptions.Services;

/// <summary>
/// Provides the permissions and default role stereotypes for the subscriptions module.
/// </summary>
public class SubscriptionPermissionsProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        SubscriptionPermissions.ManageSubscriptionSettings,
        SubscriptionPermissions.ManageSubscriptions,
        SubscriptionPermissions.ManageOwnSubscriptions,
    ];

    /// <summary>
    /// Gets all permissions exposed by the subscriptions module.
    /// </summary>
    /// <returns>The permissions exposed by the subscriptions module.</returns>
    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    /// <summary>
    /// Gets the default role stereotypes for subscriptions permissions.
    /// </summary>
    /// <returns>The default role stereotypes for subscriptions permissions.</returns>
    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new PermissionStereotype
        {
            Name = OrchardCoreConstants.Roles.Administrator,
            Permissions = _allPermissions,
        },
        new PermissionStereotype
        {
            Name = OrchardCoreConstants.Roles.Authenticated,
            Permissions =
            [
                SubscriptionPermissions.ManageOwnSubscriptions,
            ],
        },
    ];
}
