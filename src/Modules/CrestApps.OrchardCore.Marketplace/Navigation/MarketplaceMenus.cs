using System.Security.Claims;
using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Marketplace.Controllers;
using CrestApps.OrchardCore.Marketplace.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Mvc.Core.Utilities;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Marketplace.Navigation;

/// <summary>
/// Commerce › Vendors and Settings › Commerce › Marketplace.
/// </summary>
public sealed class MarketplaceAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _vendors = new()
    {
        { "area", MarketplaceConstants.Feature },
        { "controller", typeof(VendorsAdminController).ControllerName() },
        { "action", nameof(VendorsAdminController.Index) },
    };

    internal readonly IStringLocalizer S;

    public MarketplaceAdminMenu(IStringLocalizer<MarketplaceAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Commerce"], commerce => commerce
                .Add(S["Vendors"], S["Vendors"].PrefixPosition("2.7"), vendors => vendors
                    .Id("commerceVendors")
                    .Action(_vendors)
                    .Permission(MarketplacePermissions.ManageMarketplace)
                    .LocalNav()))
            .Add(S["Settings"], settings => settings
                .Add(S["Commerce"], S["Commerce"].PrefixPosition(), commerce => commerce
                    .Add(S["Marketplace"], S["Marketplace"].PrefixPosition(), marketplace => marketplace
                        .Id("marketplaceSettings")
                        .Action("Index", "Admin", new { area = "OrchardCore.Settings", groupId = MarketplaceConstants.SettingsGroupId })
                        .Permission(MarketplacePermissions.ManageMarketplace)
                        .LocalNav())));

        return ValueTask.CompletedTask;
    }
}
