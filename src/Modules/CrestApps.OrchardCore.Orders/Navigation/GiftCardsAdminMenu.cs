using CrestApps.OrchardCore.Orders.Controllers;
using CrestApps.OrchardCore.Orders.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Mvc.Core.Utilities;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Orders.Navigation;

public sealed class GiftCardsAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _routeValues = new()
    {
        { "area", OrderConstants.Features.Orders },
        { "controller", typeof(GiftCardsAdminController).ControllerName() },
        { "action", nameof(GiftCardsAdminController.Index) },
    };

    internal readonly IStringLocalizer S;

    public GiftCardsAdminMenu(IStringLocalizer<GiftCardsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Commerce"], S["Commerce"].PrefixPosition(), commerce => commerce
                .Add(S["Gift cards"], S["Gift cards"].PrefixPosition("5.5"), giftCards => giftCards
                    .AddClass("gift-cards")
                    .Id("giftCards")
                    .Action(_routeValues)
                    .Permission(OrderPermissions.ManageGiftCards)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
