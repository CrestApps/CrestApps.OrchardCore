using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Commerce.Webhooks;

public static class WebhookPermissions
{
    public static readonly Permission ManageWebhooks = new("ManageCommerceWebhooks", "Manage commerce webhooks");
}

internal sealed class WebhookPermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        WebhookPermissions.ManageWebhooks,
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
