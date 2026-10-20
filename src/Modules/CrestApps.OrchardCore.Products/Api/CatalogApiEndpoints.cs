using CrestApps.OrchardCore.Commerce.Api;
using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Products.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Products.Api;

/// <summary>
/// The catalog API: products as the storefront shows them, priced for the caller, with their variants and stock.
/// </summary>
internal static class CatalogApiEndpoints
{
    public const string Tag = "Catalog";

    public static IEndpointRouteBuilder MapCatalogApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapCommerceApi(Tag);

        group.MapGet("products", ListAsync)
            .AllowGuests()
            .WithName("ListProducts")
            .WithSummary("Lists the products the caller may see, searched, filtered by type, and sorted.");

        group.MapGet("products/{id}", GetAsync)
            .AllowGuests()
            .WithName("GetProduct")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Gets a product with its prices, variants, add-ons, specifications and stock.");

        return routes;
    }

    private static async Task<Ok<ApiPage<ApiProductSummary>>> ListAsync(
        string q,
        string type,
        string sort,
        int? skip,
        int? take,
        HttpContext httpContext,
        IProductSearchService searchService,
        ProductAttributeCatalog attributeCatalog,
        IContentManager contentManager,
        IAuthorizationService authorizationService,
        CatalogApiProductReader reader)
    {
        var types = new List<string>();

        foreach (var definition in await attributeCatalog.GetProductTypesAsync())
        {
            // Checked once per type, as the listing pages do: a caller never sees a type they may not view.
            if ((string.IsNullOrEmpty(type) || string.Equals(type, definition.Name, StringComparison.Ordinal)) &&
                await authorizationService.AuthorizeAsync(httpContext.User, CommonPermissions.ViewContent, new ContentItem { ContentType = definition.Name }))
            {
                types.Add(definition.Name);
            }
        }

        var pageSkip = ApiPage<ApiProductSummary>.ClampSkip(skip);
        var pageTake = ApiPage<ApiProductSummary>.ClampTake(take);

        if (types.Count == 0)
        {
            return TypedResults.Ok(new ApiPage<ApiProductSummary>([], 0, pageSkip, pageTake));
        }

        var result = await searchService.SearchAsync(new ProductSearchQuery
        {
            ContentTypes = types,
            Keyword = string.IsNullOrWhiteSpace(q) ? null : q.Trim(),
            Sort = GetSort(sort),
            Skip = pageSkip,
            Take = pageTake,
        }, httpContext.RequestAborted);

        var items = await contentManager.GetAsync(result.ContentItemIds, VersionOptions.Published);
        var byId = items.ToDictionary(item => item.ContentItemId, StringComparer.Ordinal);
        var summaries = new List<ApiProductSummary>();

        foreach (var id in result.ContentItemIds)
        {
            if (byId.TryGetValue(id, out var item) && await reader.ReadSummaryAsync(item, httpContext) is { } summary)
            {
                summaries.Add(summary);
            }
        }

        return TypedResults.Ok(new ApiPage<ApiProductSummary>(summaries, result.Total, pageSkip, pageTake));
    }

    private static async Task<Results<Ok<ApiProduct>, ProblemHttpResult>> GetAsync(
        string id,
        HttpContext httpContext,
        IContentManager contentManager,
        IAuthorizationService authorizationService,
        CatalogApiProductReader reader)
    {
        var item = await contentManager.GetAsync(id, VersionOptions.Published);

        if (item is null ||
            !await authorizationService.AuthorizeAsync(httpContext.User, CommonPermissions.ViewContent, item) ||
            await reader.ReadAsync(item, httpContext) is not { } product)
        {
            return ApiProblems.NotFound();
        }

        return TypedResults.Ok(product);
    }

    // The sorts the listing pages offer, by the same keys.
    private static ProductListingSort GetSort(string sort)
        => sort switch
        {
            "newest" => new() { Key = sort, AttributeKey = ProductAttributeKeys.Published, Descending = true },
            "price-asc" => new() { Key = sort, AttributeKey = ProductAttributeKeys.Price },
            "price-desc" => new() { Key = sort, AttributeKey = ProductAttributeKeys.Price, Descending = true },
            "name" => new() { Key = sort, AttributeKey = ProductAttributeKeys.Title },
            "featured" => new() { Key = sort, AttributeKey = ProductAttributeKeys.Featured, Descending = true },
            _ => new() { Key = ProductListingService.RecommendedSortKey, AttributeKey = ProductAttributeKeys.Position },
        };
}

/// <summary>
/// Reads a product into the catalog API's shapes, priced for the caller the way the product page prices it.
/// </summary>
internal sealed class CatalogApiProductReader
{
    private readonly ProductSearchDocumentBuilder _documentBuilder;
    private readonly ProductListingService _listingService;
    private readonly ProductAttributeCatalog _attributeCatalog;
    private readonly IPriceResolver _priceResolver;
    private readonly IEnumerable<IProductAvailabilityProvider> _availabilityProviders;
    private readonly IContentManager _contentManager;
    private readonly LinkGenerator _linkGenerator;
    private readonly IClock _clock;

    public CatalogApiProductReader(
        ProductSearchDocumentBuilder documentBuilder,
        ProductListingService listingService,
        ProductAttributeCatalog attributeCatalog,
        IPriceResolver priceResolver,
        IEnumerable<IProductAvailabilityProvider> availabilityProviders,
        IContentManager contentManager,
        LinkGenerator linkGenerator,
        IClock clock)
    {
        _documentBuilder = documentBuilder;
        _listingService = listingService;
        _attributeCatalog = attributeCatalog;
        _priceResolver = priceResolver;
        _availabilityProviders = availabilityProviders;
        _contentManager = contentManager;
        _linkGenerator = linkGenerator;
        _clock = clock;
    }

    public async Task<ApiProductSummary> ReadSummaryAsync(ContentItem item, HttpContext httpContext)
    {
        if (await _documentBuilder.BuildAsync(item) is not { } document)
        {
            return null;
        }

        var price = await ResolveAsync(item, variantId: null, priceId: null, httpContext.RequestAborted);

        return new ApiProductSummary(
            item.ContentItemId,
            item.ContentType,
            document.Title ?? item.DisplayText,
            document.Sku,
            price ?? ApiMoney.From(document.Price, document.Currency),
            ApiMoney.From(document.CompareAtPrice, document.Currency),
            document.IsPurchasable,
            _listingService.GetImageUrl(document.ImagePath),
            await GetUrlAsync(item, httpContext));
    }

    public async Task<ApiProduct> ReadAsync(ContentItem item, HttpContext httpContext)
    {
        if (await _documentBuilder.BuildAsync(item) is not { } document)
        {
            return null;
        }

        var cancellationToken = httpContext.RequestAborted;
        var now = _clock.UtcNow;
        var currency = document.Currency;
        var part = item.TryGet<ProductPart>(out var productPart) ? productPart : null;
        var variantsPart = item.TryGet<ProductVariantsPart>(out var variantsPartValue) ? variantsPartValue : null;
        var variants = variantsPart?.GetAvailable().ToList() ?? [];

        var stockKeys = variants.Count > 0
            ? variants.Select(variant => new ProductStockKey(item.ContentItemId, variant.VariantId)).ToArray()
            : [new ProductStockKey(item.ContentItemId, null)];
        var stock = await _availabilityProviders.GetAvailabilityAsync(stockKeys, cancellationToken: cancellationToken);

        var images = new List<ApiProductImage>();

        if (await _listingService.GetGalleryFieldAsync(item) is { } gallery)
        {
            for (var i = 0; i < gallery.Paths.Count; i++)
            {
                images.Add(new ApiProductImage(
                    _listingService.GetImageUrl(gallery.Paths[i], 1200, 0),
                    _listingService.GetImageUrl(gallery.Paths[i], 160, 160),
                    i < gallery.Texts.Count && !string.IsNullOrWhiteSpace(gallery.Texts[i]) ? gallery.Texts[i] : item.DisplayText));
            }
        }

        var prices = ((item.TryGet<ProductPricePart>(out var pricePart) ? pricePart.Prices : null) ?? [])
            .Where(price => price.IsAvailable(now))
            .Select(price => new ApiProductPrice(
                price.PriceId,
                price.Name,
                ApiMoney.From(price.Amount, price.Currency),
                price.Kind == PriceKind.Recurring ? "recurring" : "oneTime",
                price.Kind == PriceKind.Recurring ? ToCamel(price.Interval?.ToString()) : null,
                price.Kind == PriceKind.Recurring ? price.BillingDuration : null,
                price.TrialDays,
                ApiMoney.From(price.SetupFee, price.Currency),
                price.IsDefault,
                price.AllowCustomAmount,
                ApiMoney.From(price.MinimumAmount, price.Currency),
                ApiMoney.From(price.MaximumAmount, price.Currency)))
            .ToList();

        var optionNames = variantsPart?.Options?.Select(option => option.Name).ToArray();
        var apiVariants = new List<ApiProductVariant>();

        foreach (var variant in variants)
        {
            apiVariants.Add(new ApiProductVariant(
                variant.VariantId,
                variant.GetTitle(optionNames),
                variant.Options,
                variant.Sku ?? part?.Sku,
                await ResolveAsync(item, variant.VariantId, priceId: null, cancellationToken) ?? ApiMoney.From(ProductSale.GetPrice(part, variant, now), currency),
                ApiMoney.From(ProductSale.GetCompareAtPrice(part, variant, now), currency),
                _listingService.GetImageUrl(variant.ImagePath),
                ToAvailability(stock.GetValueOrDefault(new ProductStockKey(item.ContentItemId, variant.VariantId)))));
        }

        var attributes = await _attributeCatalog.GetAttributesAsync([item.ContentType]);
        var specifications = _listingService.BuildSpecifications(document, attributes, settings => settings.ShowInSpecifications)
            .Select(row => new ApiProductAttribute(row.Key, row.Label, row.Value))
            .ToList();

        var availability = variants.Count > 0
            ? ToAvailability(ProductAvailabilityProviderExtensions.Combine(stock.Values))
            : ToAvailability(stock.GetValueOrDefault(stockKeys[0]));

        return new ApiProduct(
            item.ContentItemId,
            item.ContentType,
            document.Title ?? item.DisplayText,
            document.Sku,
            await ResolveAsync(item, variantId: null, priceId: null, cancellationToken) ?? ApiMoney.From(document.Price, currency),
            ApiMoney.From(document.CompareAtPrice, currency),
            document.IsPurchasable,
            await GetUrlAsync(item, httpContext),
            images,
            prices,
            variantsPart?.Options?.Select(option => new ApiProductOption(option.Name, option.Values?.ToArray() ?? [])).ToList() ?? [],
            apiVariants,
            (part?.AddOns ?? [])
                .Select(addOn => new ApiProductAddOn(
                    addOn.AddOnId,
                    addOn.Label,
                    addOn.Kind == ProductAddOnKind.Checkbox ? "checkbox" : "text",
                    ApiMoney.From(addOn.Price, currency),
                    addOn.Required,
                    addOn.Kind == ProductAddOnKind.Text ? ProductAddOn.MaxTextLength : null))
                .ToList(),
            specifications,
            new ApiQuantityRules(Math.Max(1, part?.MinQuantity ?? 1), part?.MaxQuantity, Math.Max(1, part?.QuantityStep ?? 1)),
            availability);
    }

    // The price the caller pays for one: the sale, their role's discount and the variant's own price included. A
    // product that can't be priced (no currency, say) answers null, and the list price is shown instead.
    private async Task<ApiMoney> ResolveAsync(ContentItem item, string variantId, string priceId, CancellationToken cancellationToken)
    {
        var price = await _priceResolver.ResolveAsync(new ProductSnapshotContext(item)
        {
            VariantId = variantId,
            PriceId = priceId,
            Quantity = 1,
        }, cancellationToken);

        return price is null ? null : ApiMoney.From(price.UnitPrice, price.Currency);
    }

    private async Task<string> GetUrlAsync(ContentItem item, HttpContext httpContext)
    {
        var metadata = await _contentManager.PopulateAspectAsync<ContentItemMetadata>(item);

        return metadata.DisplayRouteValues is null
            ? null
            : _linkGenerator.GetPathByRouteValues(httpContext, routeName: null, metadata.DisplayRouteValues);
    }

    // A product nobody tracks stock for is always in stock.
    internal static ApiAvailability ToAvailability(ProductAvailability availability)
    {
        if (availability is null)
        {
            return new ApiAvailability("inStock", null, true, null);
        }

        var status = availability.Status;

        return new ApiAvailability(
            ToCamel(status.ToString()),
            status == ProductAvailabilityStatus.LowStock ? availability.Available : null,
            availability.CanBuy(1),
            availability.ReleaseDateUtc);
    }

    private static string ToCamel(string value)
        => string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value[1..];
}
