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
/// Adds the vendor's page to the storefront account, for users who run a vendor.
/// </summary>
public sealed class MarketplaceAccountMenu : NamedNavigationProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IVendorStore _vendors;

    internal readonly IStringLocalizer S;

    public MarketplaceAccountMenu(IHttpContextAccessor httpContextAccessor, IVendorStore vendors, IStringLocalizer<MarketplaceAccountMenu> stringLocalizer)
        : base(CommerceConstants.Account.MenuName)
    {
        _httpContextAccessor = httpContextAccessor;
        _vendors = vendors;
        S = stringLocalizer;
    }

    protected override async ValueTask BuildAsync(NavigationBuilder builder)
    {
        if (_httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier) is not { } userId ||
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
