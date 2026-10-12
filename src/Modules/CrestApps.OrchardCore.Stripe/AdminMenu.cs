using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Drivers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Stripe;

/// <summary>
/// Adds the Stripe settings entry to the Orchard Core admin navigation.
/// </summary>
public sealed class AdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _routeValues = new()
    {
        { "area", "OrchardCore.Settings" },
        { "groupId", StripeSettingsDisplayDriver.GroupId },
    };

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminMenu"/> class.
    /// </summary>
    /// <param name="localizer">The localizer used to build menu labels.</param>
    public AdminMenu(IStringLocalizer<AdminMenu> localizer)
    {
        S = localizer;
    }

    /// <summary>
    /// Builds the Stripe admin navigation entries.
    /// </summary>
    /// <param name="builder">The navigation builder to update.</param>
    /// <returns>A completed task after the navigation entries are registered.</returns>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
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
            );

        return ValueTask.CompletedTask;
    }
}
