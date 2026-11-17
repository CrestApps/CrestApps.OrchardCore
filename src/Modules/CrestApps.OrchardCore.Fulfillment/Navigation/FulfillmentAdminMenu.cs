using CrestApps.OrchardCore.Fulfillment.Controllers;
using CrestApps.OrchardCore.Shipping;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Mvc.Core.Utilities;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Fulfillment.Navigation;

/// <summary>
/// Commerce › Fulfillment requests, for people who ship orders, and Settings › Commerce › Fulfillment partners, for
/// people who manage the partners.
/// </summary>
internal sealed class FulfillmentAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _requestsRouteValues = new()
    {
        { "area", FulfillmentConstants.Features.Fulfillment },
        { "controller", typeof(FulfillmentAdminController).ControllerName() },
        { "action", nameof(FulfillmentAdminController.Index) },
    };

    internal readonly IStringLocalizer S;

    public FulfillmentAdminMenu(IStringLocalizer<FulfillmentAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Commerce"], commerce => commerce
                .Add(S["Fulfillment requests"], S["Fulfillment requests"].PrefixPosition(), requests => requests
                    .Id("fulfillmentRequests")
                    .Action(_requestsRouteValues)
                    .Permission(ShippingPermissions.FulfillOrders)
                    .LocalNav()))
            .Add(S["Settings"], settings => settings
                .Add(S["Commerce"], S["Commerce"].PrefixPosition(), commerce => commerce
                    .Add(S["Fulfillment partners"], S["Fulfillment partners"].PrefixPosition(), partners => partners
                        .Id("fulfillmentPartnersSettings")
                        .Action("Index", "Admin", new { area = "OrchardCore.Settings", groupId = FulfillmentConstants.SettingsGroupId })
                        .Permission(FulfillmentPermissions.ManageFulfillmentPartners)
                        .LocalNav())));

        return ValueTask.CompletedTask;
    }
}
