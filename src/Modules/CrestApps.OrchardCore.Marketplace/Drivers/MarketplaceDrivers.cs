using System.Globalization;
using CrestApps.OrchardCore.Marketplace.Models;
using CrestApps.OrchardCore.Marketplace.Services;
using CrestApps.OrchardCore.Marketplace.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Marketplace.Drivers;

/// <summary>
/// The Vendor part's editor: which vendor a product is sold for.
/// </summary>
internal sealed class VendorPartDisplayDriver : ContentPartDisplayDriver<VendorPart>
{
    private readonly IVendorStore _vendors;

    public VendorPartDisplayDriver(IVendorStore vendors)
    {
        _vendors = vendors;
    }

    public override IDisplayResult Edit(VendorPart part, BuildPartEditorContext context)
        => Initialize<VendorPartViewModel>(GetEditorShapeType(context), async model =>
        {
            model.VendorId = part.VendorId;
            model.Vendors = [.. (await _vendors.ListAllAsync()).Select(vendor => (vendor.ItemId, vendor.Name))];
        });

    public override async Task<IDisplayResult> UpdateAsync(VendorPart part, UpdatePartEditorContext context)
    {
        var model = new VendorPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        part.VendorId = string.IsNullOrEmpty(model.VendorId) || await _vendors.FindByIdAsync(model.VendorId) is null ? null : model.VendorId;

        return Edit(part, context);
    }
}

/// <summary>
/// Renders a vendor as a row of the vendors admin list, in both of its layouts (List and Grid).
/// </summary>
internal sealed class VendorDisplayDriver : DisplayDriver<Vendor>
{
    public override IDisplayResult Display(Vendor vendor, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = vendor.Name?.ToLowerInvariant() ?? string.Empty;
        }

        return Combine(
            View("Vendor_Fields_SummaryAdmin", vendor)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("Vendor_Description_SummaryAdmin", vendor)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Payouts:1"),
            View("Vendor_Buttons_SummaryAdmin", vendor).Location("Actions:5"));
    }
}

/// <summary>
/// Settings › Commerce › Marketplace: the store's commission and when vendors' earnings are released.
/// </summary>
internal sealed class MarketplaceSettingsDisplayDriver : SiteDisplayDriver<MarketplaceSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    internal readonly IStringLocalizer S;

    public MarketplaceSettingsDisplayDriver(IHttpContextAccessor httpContextAccessor, IAuthorizationService authorizationService, IStringLocalizer<MarketplaceSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    protected override string SettingsGroupId
        => MarketplaceConstants.SettingsGroupId;

    public override IDisplayResult Edit(ISite site, MarketplaceSettings settings, BuildEditorContext context)
        => Initialize<MarketplaceSettingsViewModel>("MarketplaceSettings_Edit", model =>
        {
            model.CommissionPercent = settings.CommissionPercent.ToString(CultureInfo.InvariantCulture);
            model.HoldDays = settings.HoldDays;
            model.ReleaseOnFulfillment = settings.ReleaseOnFulfillment;
            model.AutomaticPayouts = settings.AutomaticPayouts;
            model.DefaultCountry = settings.DefaultCountry;
        }).Location("Content:1")
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, MarketplacePermissions.ManageMarketplace))
        .OnGroup(SettingsGroupId);

    public override async Task<IDisplayResult> UpdateAsync(ISite site, MarketplaceSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, MarketplacePermissions.ManageMarketplace))
        {
            return null;
        }

        var model = new MarketplaceSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (!decimal.TryParse(model.CommissionPercent?.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var commission) || commission is < 0m or > 100m)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.CommissionPercent), S["Enter the commission as a percent from 0 to 100, such as 10 or 12.5."]);
        }
        else
        {
            settings.CommissionPercent = commission;
        }

        settings.HoldDays = Math.Clamp(model.HoldDays, 0, 365);
        settings.ReleaseOnFulfillment = model.ReleaseOnFulfillment;
        settings.AutomaticPayouts = model.AutomaticPayouts;
        settings.DefaultCountry = model.DefaultCountry?.Trim().Length == 2 ? model.DefaultCountry.Trim().ToUpperInvariant() : settings.DefaultCountry;

        return Edit(site, settings, context);
    }
}
