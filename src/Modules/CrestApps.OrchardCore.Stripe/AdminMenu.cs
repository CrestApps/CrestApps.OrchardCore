using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Drivers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Stripe;

public sealed class AdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _routeValues = new()
    {
        { "area", "OrchardCore.Settings" },
        { "groupId", StripeSettingsDisplayDriver.GroupId },
    };

    internal readonly IStringLocalizer S;

    public AdminMenu(IStringLocalizer<AdminMenu> localizer)
    {
        S = localizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Configuration"], configuration => configuration
                .Add(S["Settings"], settings => settings
                   .Add(S["Payments"], S["Payments"].PrefixPosition(), payments => payments
                      .Id("payments")
                      .AddClass("payments")
                      .Add(S["Stripe"], S["Stripe"].PrefixPosition(), stripe => stripe
                          .AddClass("stripe")
                          .Id("stripe")
                          .Action("Index", "Admin", _routeValues)
                          .Permission(StripePermissions.ManageStripeSettings)
                          .LocalNav()
                       )
                    )
                )
            );

        return ValueTask.CompletedTask;
    }
}
