using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Subscriptions.Core;

public static class SubscriptionPermissions
{
    public static readonly Permission ManageSubscriptionSettings = new("ManageSubscriptionSettings", "Manage subscriptions settings");

    public static readonly Permission ManageSubscriptions = new("ManageSubscriptions", "Manage subscriptions");

    public static readonly Permission ManageOwnSubscriptions = new("ManageSubscriptions", "Manage subscriptions", [ManageSubscriptions]);
}
