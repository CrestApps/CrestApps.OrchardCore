using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Commerce.Webhooks;

public static class WebhookPermissions
{
    public static readonly Permission ManageWebhooks = new(
        "ManageCommerceWebhooks",
        LocalizationSource.Create("Manage commerce webhooks", typeof(WebhookPermissions)),
        isSecurityCritical: true);
}
