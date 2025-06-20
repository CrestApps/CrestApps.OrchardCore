using CrestApps.OrchardCore.Subscriptions.Controllers;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Drivers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Mvc.Core.Utilities;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Subscriptions.Services;

public sealed class SubscriptionsAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _routeValues = new()
    {
        { "area", "OrchardCore.Settings" },
        { "controller", "Admin" },
        { "action", "Index" },
        { "groupId", SubscriptionSettingsDisplayDriver.GroupId },
    };

    private static readonly RouteValueDictionary _subscriptionRouteValues = new()
    {
        { "area", SubscriptionConstants.Features.Area },
        { "controller", typeof(AdminController).ControllerName() },
        { "action", nameof(AdminController.Index) },
    };

    private static readonly RouteValueDictionary _subscriptionDashboardRouteValues = new()
    {
        { "area", SubscriptionConstants.Features.Area },
        { "controller", typeof(DashboardController).ControllerName() },
        { "action", nameof(DashboardController.Index) },
    };
    internal readonly IStringLocalizer S;

    public SubscriptionsAdminMenu(IStringLocalizer<SubscriptionsAdminMenu> localizer)
    {
        S = localizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Configuration"], configuration => configuration
                .Add(S["Settings"], settings => settings
                    .Add(S["Subscriptions"], S["Subscriptions"].PrefixPosition(), subscriptions => subscriptions
                        .AddClass("subscriptions")
                        .Id("subscriptions")
                        .Action(_routeValues)
                        .Permission(SubscriptionPermissions.ManageSubscriptionSettings)
                        .LocalNav()
                    )
                )
            )
            .Add(S["Subscriptions"], S["Subscriptions"].PrefixPosition(), subscriptions => subscriptions
                .AddClass("subscriptions")
                .Id("subscriptions")
                .Action(_subscriptionRouteValues)
                .Permission(SubscriptionPermissions.ManageSubscriptions)
                .LocalNav()
            )
            .Add(S["My Subscription"], S["My Subscription"].PrefixPosition(), subscriptions => subscriptions
                .AddClass("subscriberDashboard")
                .Id("subscriberDashboard")
                .Action(_subscriptionDashboardRouteValues)
                .Permission(SubscriptionPermissions.AccessSubscriberDashboard)
                .LocalNav());

        return ValueTask.CompletedTask;
    }
}
