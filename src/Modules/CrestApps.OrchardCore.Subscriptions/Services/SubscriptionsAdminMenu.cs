using CrestApps.OrchardCore.Subscriptions.Controllers;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Drivers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Mvc.Core.Utilities;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Subscriptions.Services;

/// <summary>
/// Builds the admin navigation entries for subscription settings and management.
/// </summary>
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

    private static readonly RouteValueDictionary _dashboardRouteValues = new()
    {
        { "area", SubscriptionConstants.Features.Area },
        { "controller", typeof(DashboardController).ControllerName() },
        { "action", nameof(DashboardController.Index) },
    };

    private static readonly RouteValueDictionary _agreementRouteValues = new()
    {
        { "area", SubscriptionConstants.Features.Area },
        { "controller", typeof(SubscriptionAgreementsController).ControllerName() },
        { "action", nameof(SubscriptionAgreementsController.Index) },
    };

    private static readonly RouteValueDictionary _mySubscriptionRouteValues = new()
    {
        { "area", SubscriptionConstants.Features.Area },
        { "controller", typeof(MySubscriptionsController).ControllerName() },
        { "action", nameof(MySubscriptionsController.Index) },
    };

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionsAdminMenu"/> class.
    /// </summary>
    /// <param name="localizer">The string localizer used for admin menu labels.</param>
    public SubscriptionsAdminMenu(IStringLocalizer<SubscriptionsAdminMenu> localizer)
    {
        S = localizer;
    }

    /// <summary>
    /// Builds the subscription entries in the admin navigation tree.
    /// </summary>
    /// <param name="builder">The navigation builder to update.</param>
    /// <returns>A completed task once the navigation entries have been added.</returns>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Settings"], settings => settings
                .Add(S["Subscriptions"], S["Subscriptions"].PrefixPosition(), subscriptions => subscriptions
                    .AddClass("subscriptions")
                    .Id("subscriptionsSettings")
                    .Action(_routeValues)
                    .Permission(SubscriptionPermissions.ManageSubscriptionSettings)
                    .LocalNav()
                )
            )
            .Add(S["Subscriptions"], S["Subscriptions"].PrefixPosition(), subscriptions => subscriptions
                .AddClass("subscriptions")
                .Id("subscriptions")
                .Add(S["Manage"], S["Manage"].PrefixPosition("1"), manage => manage
                    .Action(_subscriptionRouteValues)
                    .Permission(SubscriptionPermissions.ManageSubscriptions)
                    .LocalNav()
                )
                .Add(S["My Subscriptions"], S["My Subscriptions"].PrefixPosition("2"), dashboard => dashboard
                    .Action(_dashboardRouteValues)
                    .Permission(SubscriptionPermissions.ManageOwnSubscriptions)
                    .LocalNav()
                )
                .Add(S["Agreements"], S["Agreements"].PrefixPosition("3"), agreements => agreements
                    .Action(_agreementRouteValues)
                    .Permission(SubscriptionPermissions.ManageSubscriptions)
                    .LocalNav()
                )
                .Add(S["My Plans"], S["My Plans"].PrefixPosition("4"), plans => plans
                    .Action(_mySubscriptionRouteValues)
                    .Permission(SubscriptionPermissions.ManageOwnSubscriptions)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
