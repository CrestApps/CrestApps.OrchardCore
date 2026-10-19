using System.Text.Json;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Orders.Endpoints;
using CrestApps.OrchardCore.Orders.Services;
using CrestApps.OrchardCore.Orders.ViewModels;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Orders.Drivers;

/// <summary>
/// Adds the products and categories a coupon is limited to to the coupon editor.
/// </summary>
internal sealed class CouponItemLimitsDisplayDriver : DisplayDriver<Coupon>
{
    private const int MaxItems = 200;

    private readonly CouponItemLookup _lookup;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CouponItemLimitsDisplayDriver(CouponItemLookup lookup, IHttpContextAccessor httpContextAccessor)
    {
        _lookup = lookup;
        _httpContextAccessor = httpContextAccessor;
    }

    public override IDisplayResult Edit(Coupon coupon, BuildEditorContext context)
        => Initialize<CouponItemLimitsViewModel>("CouponItemLimits_Edit", async model =>
        {
            var pathBase = _httpContextAccessor.HttpContext?.Request.PathBase.Value ?? string.Empty;

            model.ProductIds = [.. coupon.ProductIds ?? []];
            model.CategoryIds = [.. coupon.CategoryIds ?? []];
            model.ProductSearchEndpoint = $"{pathBase}/{CouponItemSearchEndpoints.ProductsPath}";
            model.CategorySearchEndpoint = $"{pathBase}/{CouponItemSearchEndpoints.CategoriesPath}";
            model.InitialProductsJson = ToJson(await _lookup.GetProductsAsync(model.ProductIds));
            model.InitialCategoriesJson = ToJson(await _lookup.GetCategoriesAsync(model.CategoryIds));
            model.BuyProductIds = [.. coupon.BuyProductIds ?? []];
            model.BuyCategoryIds = [.. coupon.BuyCategoryIds ?? []];
            model.InitialBuyProductsJson = ToJson(await _lookup.GetProductsAsync(model.BuyProductIds));
            model.InitialBuyCategoriesJson = ToJson(await _lookup.GetCategoriesAsync(model.BuyCategoryIds));
        }).Location("Content:10");

    public override async Task<IDisplayResult> UpdateAsync(Coupon coupon, UpdateEditorContext context)
    {
        var model = new CouponItemLimitsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        coupon.ProductIds = Clean(model.ProductIds);
        coupon.CategoryIds = Clean(model.CategoryIds);
        coupon.BuyProductIds = Clean(model.BuyProductIds);
        coupon.BuyCategoryIds = Clean(model.BuyCategoryIds);

        return Edit(coupon, context);
    }

    private static List<string> Clean(string[] ids)
        => [.. (ids ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).Distinct(StringComparer.Ordinal).Take(MaxItems)];

    private static string ToJson(IEnumerable<CouponItem> items)
        => JsonSerializer.Serialize(items.Select(item => new { value = item.Id, text = item.Text, secondaryText = item.Group, selected = true }));
}
