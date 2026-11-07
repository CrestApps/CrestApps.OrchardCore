using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Webhooks;

/// <summary>
/// The webhooks permissions.
/// </summary>
public static class WebhookPermissions
{
    /// <summary>
    /// Manage webhook subscriptions and their deliveries. The name is the one Commerce Webhooks released, so the roles
    /// that were granted it keep it.
    /// </summary>
    public static readonly Permission ManageWebhooks = new(
        "ManageCommerceWebhooks",
        LocalizationSource.Create("Manage webhooks", typeof(WebhookPermissions)),
        isSecurityCritical: true);
}
