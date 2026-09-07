using CrestApps.OrchardCore.Checkout.Controllers;
using CrestApps.OrchardCore.Checkout.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Mvc.Core.Utilities;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Checkout.Navigation;

/// <summary>
/// Adds the coupon catalog to the Commerce admin menu.
/// </summary>
public sealed class CouponsAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _routeValues = new()
    {
        { "area", CheckoutConstants.Features.Area },
        { "controller", typeof(CouponsAdminController).ControllerName() },
        { "action", nameof(CouponsAdminController.Index) },
    };

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CouponsAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CouponsAdminMenu(IStringLocalizer<CouponsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Commerce"], S["Commerce"].PrefixPosition(), commerce => commerce
                .Add(S["Coupons"], S["Coupons"].PrefixPosition("9"), coupons => coupons
                    .AddClass("coupons")
                    .Id("coupons")
                    .Action(_routeValues)
                    .Permission(CheckoutPermissions.ManageCoupons)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
