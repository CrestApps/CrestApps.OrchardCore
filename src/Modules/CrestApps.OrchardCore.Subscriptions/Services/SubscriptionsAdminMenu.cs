using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Drivers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Subscriptions.Services;

public sealed class SubscriptionsAdminMenu : INavigationProvider
{
    private static readonly RouteValueDictionary _routeValues = new()
    {
        { "area", "OrchardCore.Settings" },
        { "groupId", SubscriptionSettingsDisplayDriver.GroupId },
    };

    internal readonly IStringLocalizer S;

    public SubscriptionsAdminMenu(IStringLocalizer<SubscriptionsAdminMenu> localizer)
    {
        S = localizer;
    }

    public Task BuildNavigationAsync(string name, NavigationBuilder builder)
    {
        if (!NavigationHelper.IsAdminMenu(name))
        {
            return Task.CompletedTask;
        }

        builder
            .Add(S["Configuration"], configuration => configuration
                .Add(S["Settings"], settings => settings
                    .Add(S["Subscriptions"], S["Subscriptions"].PrefixPosition(), subscriptions => subscriptions
                        .AddClass("subscriptions")
                        .Id("subscriptions")
                        .Action("Index", "Admin", _routeValues)
                        .Permission(SubscriptionPermissions.ManageSubscriptionsSettings)
                        .LocalNav()
                    )
                )
            );

        return Task.CompletedTask;
    }
}