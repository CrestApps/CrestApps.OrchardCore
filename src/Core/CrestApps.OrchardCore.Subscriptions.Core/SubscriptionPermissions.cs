using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Subscriptions.Core;

public static class SubscriptionPermissions
{
    public static readonly Permission ManageSubscriptionSettings = new("ManageSubscriptionSettings", "Manage subscriptions settings");

    public static readonly Permission ManageSubscriptions = new("ManageSubscriptions", "Manage subscriptions");

    public static readonly Permission ManageOwnSubscriptions = new("ManageOwnSubscriptions", "Manage Own subscriptions", [ManageSubscriptions]);

    public static readonly Permission AccessSubscriberDashboard = new("AccessSubscriberDashboard", "Access Subscriber Dashboard");
}
