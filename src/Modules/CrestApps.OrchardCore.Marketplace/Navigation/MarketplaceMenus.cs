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
            .Add(S["Commerce"], S["Commerce"].PrefixPosition(), commerce => commerce
                .Add(S["Vendors"], S["Vendors"].PrefixPosition("2.7"), vendors => vendors
                    .AddClass("commerce-vendors")
                    .Id("commerceVendors")
                    .Action(_vendors)
                    .Permission(MarketplacePermissions.ManageMarketplace)
                    .LocalNav()))
            .Add(S["Settings"], settings => settings
                .Add(S["Commerce"], S["Commerce"].PrefixPosition(), commerce => commerce
                    .Add(S["Marketplace"], S["Marketplace"].PrefixPosition(), marketplace => marketplace
                        .AddClass("marketplace-settings")
                        .Id("marketplaceSettings")
                        .Action("Index", "Admin", new { area = "OrchardCore.Settings", groupId = MarketplaceConstants.SettingsGroupId })
                        .Permission(MarketplacePermissions.ManageMarketplace)
                        .LocalNav())));

        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Adds the vendor's page to the storefront account, for users who run a vendor.
/// </summary>
public sealed class MarketplaceAccountMenu : INavigationProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IVendorStore _vendors;

    internal readonly IStringLocalizer S;

    public MarketplaceAccountMenu(IHttpContextAccessor httpContextAccessor, IVendorStore vendors, IStringLocalizer<MarketplaceAccountMenu> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _vendors = vendors;
        S = stringLocalizer;
    }

    public async ValueTask BuildNavigationAsync(string name, NavigationBuilder builder)
    {
        if (!string.Equals(name, CommerceConstants.Account.MenuName, StringComparison.Ordinal) ||
            _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier) is not { } userId ||
            (await _vendors.ListByUserAsync(userId)).Count == 0)
        {
            return;
        }

        builder.Add(S["Vendor"], "22", vendor => vendor
            .Id("account-vendor")
            .AddClass("fa-solid fa-store")
            .Action("Index", "VendorPortal", new { area = MarketplaceConstants.Feature }));
    }
}
