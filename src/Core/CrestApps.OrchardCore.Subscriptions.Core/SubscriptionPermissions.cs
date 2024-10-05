using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Subscriptions.Core;

public static class SubscriptionPermissions
{
    public static readonly Permission ManageSubscriptionsSettings = new("ManageSubscriptionsSettings", "Manage subscriptions settings");
}
