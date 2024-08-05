using CrestApps.OrchardCore.Stripe.Core;
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Stripe;

public sealed class StripePermissionsProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        StripePermissions.ManageStripeSettings,
    ];

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new PermissionStereotype
        {
            Name = OrchardCoreConstants.Roles.Administrator,
            Permissions = _allPermissions,
        },
    ];
}

