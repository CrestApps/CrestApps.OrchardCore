using System.Text.Json;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Endpoints;
using CrestApps.OrchardCore.Products.Services;
using CrestApps.OrchardCore.Products.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Products.Drivers;

/// <summary>
/// The upsell and cross-sell pickers on a product's editor, and on its page: "You may also like" (its upsells),
/// related products from the same categories, and the shopper's recently viewed products.
/// </summary>
public sealed class ProductRecommendationsDisplayDriver : ContentPartDisplayDriver<ProductPart>
{
    private const int MaxPicked = 12;

    private readonly ProductRecommendationService _recommendations;
    private readonly ProductListingService _listing;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly LinkGenerator _linkGenerator;

    internal readonly IStringLocalizer S;

    public ProductRecommendationsDisplayDriver(
        ProductRecommendationService recommendations,
        ProductListingService listing,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<ProductRecommendationsDisplayDriver> stringLocalizer,
        LinkGenerator linkGenerator)
    {
        _recommendations = recommendations;
        _listing = listing;
        _httpContextAccessor = httpContextAccessor;
        _linkGenerator = linkGenerator;
        S = stringLocalizer;
    }

    public override IDisplayResult Display(ProductPart part, BuildPartDisplayContext context)
    {
        if (context.DisplayType != "Detail")
        {
            return null;
        }

        var item = part.ContentItem;
        var upsellIds = item.TryGet<ProductRecommendationsPart>(out var recommendations) ? (recommendations.UpsellIds ?? []) : [];

        return Combine(
            Initialize<ProductCardStripViewModel>("ProductUpsells", async model =>
            {
                model.Heading = S["You may also like"];
                model.Cards = await _listing.BuildCardsAsync([.. upsellIds.Where(id => id != item.ContentItemId).Take(ProductRecommendationService.MaxItems)]);
            }).Location("Detail", "Content:58"),
            Initialize<ProductCardStripViewModel>("ProductRelated", async model =>
            {
                model.Heading = S["Related products"];
                model.Cards = await _listing.BuildCardsAsync(await _recommendations.GetRelatedIdsAsync(item.ContentItemId, upsellIds));
            }).Location("Detail", "Content:70"),
            Initialize<ProductRecentlyViewedViewModel>("ProductRecentlyViewed", model =>
            {
                model.ContentItemId = item.ContentItemId;
            }).Location("Detail", "Content:80"));
    }

    public override IDisplayResult Edit(ProductPart part, BuildPartEditorContext context)
        => Initialize<ProductRecommendationsEditViewModel>("ProductRecommendations_Edit", async model =>
        {
            var recommendations = part.ContentItem.TryGet<ProductRecommendationsPart>(out var found) ? found : new ProductRecommendationsPart();
            model.UpsellIds = [.. recommendations.UpsellIds ?? []];
            model.CrossSellIds = [.. recommendations.CrossSellIds ?? []];
            model.SearchEndpoint = _linkGenerator.GetPathByName(_httpContextAccessor.HttpContext, ProductSearchEndpoints.RouteName);
            model.InitialUpsellsJson = ToJson(await _recommendations.NameAsync(model.UpsellIds));
            model.InitialCrossSellsJson = ToJson(await _recommendations.NameAsync(model.CrossSellIds));
        }).Location("Content:20");

    public override async Task<IDisplayResult> UpdateAsync(ProductPart part, UpdatePartEditorContext context)
    {
        var model = new ProductRecommendationsEditViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var self = part.ContentItem.ContentItemId;

        part.ContentItem.Alter<ProductRecommendationsPart>(recommendations =>
        {
            recommendations.UpsellIds = Clean(model.UpsellIds, self);
            recommendations.CrossSellIds = Clean(model.CrossSellIds, self);
        });

        return Edit(part, context);
    }

    // A product doesn't recommend itself, and each product is listed once.
    private static List<string> Clean(IEnumerable<string> ids, string self)
        => [.. (ids ?? []).Where(id => !string.IsNullOrWhiteSpace(id) && id != self).Select(id => id.Trim()).Distinct(StringComparer.Ordinal).Take(MaxPicked)];

    private static string ToJson(IEnumerable<(string Id, string Text)> items)
        => JsonSerializer.Serialize(items.Select(item => new { value = item.Id, text = item.Text, selected = true }));
}
