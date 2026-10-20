using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Products.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Media;
using OrchardCore.Modules;
using OrchardCore.Settings;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Products.Feeds;

/// <summary>
/// Builds the store's product feed: every published product sold online, and each enabled variant of one with variants,
/// priced in the store's currency or another one converted, with its photos, availability, brand and identifiers.
/// An item Merchant Center would refuse is left out, with why.
/// </summary>
public sealed class ProductFeedBuilder
{
    private const int BatchSize = 200;

    private readonly ISession _session;
    private readonly IContentManager _contentManager;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ProductContentTypes _productTypes;
    private readonly ISiteService _siteService;
    private readonly IServiceProvider _serviceProvider;
    private readonly LinkGenerator _linkGenerator;
    private readonly IClock _clock;

    public ProductFeedBuilder(
        ISession session,
        IContentManager contentManager,
        IContentDefinitionManager contentDefinitionManager,
        ProductContentTypes productTypes,
        ISiteService siteService,
        IServiceProvider serviceProvider,
        LinkGenerator linkGenerator,
        IClock clock)
    {
        _session = session;
        _contentManager = contentManager;
        _contentDefinitionManager = contentDefinitionManager;
        _productTypes = productTypes;
        _siteService = siteService;
        _serviceProvider = serviceProvider;
        _linkGenerator = linkGenerator;
        _clock = clock;
    }

    /// <summary>
    /// Builds the feed, priced in <paramref name="currency"/>, or the store's when none.
    /// </summary>
    public async Task<ProductFeed> BuildAsync(string currency = null, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var site = await _siteService.GetSiteSettingsAsync();
        var settings = await _siteService.GetSettingsAsync<ProductFeedSettings>();
        var baseUrl = site.BaseUrl?.TrimEnd('/');
        var types = await _productTypes.GetProductTypesAsync(_contentDefinitionManager);
        var terms = await LoadTermsAsync(cancellationToken);
        var shopperCurrency = _serviceProvider.GetService<IShopperCurrency>();
        var availabilityProviders = _serviceProvider.GetServices<IProductAvailabilityProvider>();
        var media = _serviceProvider.GetService<IMediaFileStore>();

        var items = new List<ProductFeedItem>();
        var problems = new List<ProductFeedProblem>();

        if (types.Count == 0)
        {
            return new ProductFeed(items, problems, now);
        }

        for (var skip = 0; ; skip += BatchSize)
        {
            var batch = (await _session.Query<ContentItem, ContentItemIndex>(index => index.ContentType.IsIn(types) && index.Published)
                .OrderBy(index => index.Id)
                .Skip(skip)
                .Take(BatchSize)
                .ListAsync(cancellationToken)).ToList();

            if (batch.Count == 0)
            {
                break;
            }

            var keys = batch.Select(item => new ProductStockKey(item.ContentItemId, null)).ToList();
            var availability = await availabilityProviders.GetAvailabilityAsync(keys, cancellationToken: cancellationToken);

            foreach (var contentItem in batch)
            {
                var definition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentItem.ContentType);
                var product = contentItem.TryGet<ProductPart>(out var productPart) ? productPart : null;
                var partSettings = definition?.Parts.FirstOrDefault(part => part.PartDefinition.Name == nameof(ProductPart))?.GetSettings<ProductPartSettings>() ?? new ProductPartSettings();
                var feedPart = contentItem.TryGet<ProductFeedPart>(out var feedValues) ? feedValues : null;

                // Only what's sold here, with its price shown, goes in a feed; a subscription plan is a recurring price.
                if (product is null || feedPart?.ExcludeFromFeeds == true || !product.CanBePurchased(partSettings) ||
                    product.GetPriceDisplay(partSettings) != ProductPriceDisplay.Show ||
                    (contentItem.TryGet<ProductPricePart>(out var prices) ? prices : null)?.GetDefault(now) is { Kind: PriceKind.Recurring })
                {
                    continue;
                }

                var link = await GetLinkAsync(contentItem, baseUrl);
                var gallery = definition is null ? null : ProductGalleryReader.Find(definition, contentItem);
                var images = (gallery?.Paths ?? []).Select(path => ToAbsolute(media?.MapPathToPublicUrl(path), baseUrl)).Where(url => url is not null).ToList();
                var description = Truncate(ProductFeedValidator.ToPlainText(
                    contentItem.Content["HtmlBodyPart"]?["Html"]?.ToString() ??
                    contentItem.Content["MarkdownBodyPart"]?["Markdown"]?.ToString() ??
                    contentItem.Content["SeoMetaPart"]?["MetaDescription"]?.ToString()), ProductFeedValidator.MaxDescriptionLength);
                var fieldTerms = TermsOf(contentItem)
                    .Select(entry => (entry.Field, Term: terms.GetValueOrDefault(entry.TermId)))
                    .Where(entry => entry.Term is not null)
                    .ToList();

                // A term of a field or taxonomy named for brands is the brand; the first other term, the product type.
                bool IsBrand((string Field, FeedTerm Term) entry)
                    => Mentions(entry.Field, "brand") || Mentions(entry.Term.Taxonomy, "brand");

                var brand = feedPart?.Brand ?? fieldTerms.FirstOrDefault(IsBrand).Term?.Name ?? settings.DefaultBrand;
                var productType = fieldTerms.FirstOrDefault(entry => !IsBrand(entry)).Term?.Path;
                var productColor = TextFieldOf(contentItem, "Color", "Colour");
                var variants = contentItem.TryGet<ProductVariantsPart>(out var variantsPart) ? variantsPart : null;
                var enabled = variants?.GetAvailable().ToList() ?? [];
                var stock = availability.GetValueOrDefault(new ProductStockKey(contentItem.ContentItemId, null));

                ProductFeedItem Item(string id, string title, ProductVariant variant, ProductAvailability itemStock)
                {
                    var item = new ProductFeedItem
                    {
                        Id = Truncate(id, ProductFeedValidator.MaxIdLength),
                        ItemGroupId = variant is null ? null : contentItem.ContentItemId,
                        Title = Truncate(title, ProductFeedValidator.MaxTitleLength),
                        Description = string.IsNullOrWhiteSpace(description) ? Truncate(title, ProductFeedValidator.MaxDescriptionLength) : description,
                        Link = link,
                        ImageLink = ToAbsolute(variant?.ImagePath is { Length: > 0 } variantImage ? media?.MapPathToPublicUrl(variantImage) : null, baseUrl) ?? images.FirstOrDefault(),
                        AdditionalImageLinks = [.. images.Skip(1)],
                        Brand = brand,
                        Gtin = variant is not null && feedPart?.VariantGtins.TryGetValue(variant.VariantId, out var variantGtin) == true ? variantGtin : feedPart?.Gtin,
                        Mpn = feedPart?.Mpn,
                        Condition = feedPart?.Condition ?? settings.DefaultCondition ?? "new",
                        GoogleProductCategory = feedPart?.GoogleProductCategory ?? settings.DefaultGoogleProductCategory,
                        ProductType = productType,
                        Color = variant?.Options.FirstOrDefault(option => IsOption(option.Key, "color", "colour")).Value ?? productColor,
                        Size = variant?.Options.FirstOrDefault(option => IsOption(option.Key, "size")).Value,
                    };

                    (item.Availability, item.AvailabilityDateUtc) = itemStock?.Status switch
                    {
                        null or ProductAvailabilityStatus.InStock or ProductAvailabilityStatus.LowStock => ("in_stock", null),
                        ProductAvailabilityStatus.Preorder => ("preorder", itemStock.ReleaseDateUtc),
                        ProductAvailabilityStatus.Backorder => ("backorder", itemStock.ReleaseDateUtc),
                        _ => ("out_of_stock", (DateTime?)null),
                    };

                    return item;
                }

                var candidates = new List<(ProductFeedItem Item, ProductVariant Variant)>();

                if (enabled.Count == 0)
                {
                    candidates.Add((Item(product.Sku is { Length: > 0 } sku ? sku : contentItem.ContentItemId, contentItem.DisplayText, null, stock), null));
                }
                else
                {
                    var optionNames = variants.Options.Select(option => option.Name).ToList();
                    var variantStock = await availabilityProviders.GetAvailabilityAsync([.. enabled.Select(variant => new ProductStockKey(contentItem.ContentItemId, variant.VariantId))], cancellationToken: cancellationToken);

                    foreach (var variant in enabled)
                    {
                        var variantTitle = variant.GetTitle(optionNames);
                        var title = string.IsNullOrEmpty(variantTitle) ? contentItem.DisplayText : $"{contentItem.DisplayText} - {variantTitle}";

                        candidates.Add((Item(variant.Sku is { Length: > 0 } sku ? sku : $"{contentItem.ContentItemId}-{variant.VariantId}", title, variant, variantStock.GetValueOrDefault(new ProductStockKey(contentItem.ContentItemId, variant.VariantId)) ?? stock), variant));
                    }
                }

                foreach (var (item, variant) in candidates)
                {
                    if (!settings.IncludeOutOfStock && item.Availability == "out_of_stock")
                    {
                        continue;
                    }

                    if (!await PriceAsync(item, contentItem, product, variant, now, currency, shopperCurrency, cancellationToken))
                    {
                        problems.Add(new ProductFeedProblem(item.Id, item.Title, $"it can't be priced in {currency}", LeftOut: true));

                        continue;
                    }

                    var found = ProductFeedValidator.Validate(item);

                    if (found.Count > 0)
                    {
                        problems.AddRange(found.Select(problem => new ProductFeedProblem(item.Id, item.Title, problem, LeftOut: true)));

                        continue;
                    }

                    if (!item.IdentifierExists)
                    {
                        problems.Add(new ProductFeedProblem(item.Id, item.Title, "it has no brand with a GTIN or MPN, so it's sent with identifier_exists no", LeftOut: false));
                    }

                    items.Add(item);
                }
            }

            if (batch.Count < BatchSize)
            {
                break;
            }
        }

        return new ProductFeed(items, problems, now);
    }

    // The price anyone pays, without a signed-in buyer's own: the variant's, the product's, or its default one-time
    // price; and its sale price while a sale is on or coming, all in the feed's currency.
    private static async Task<bool> PriceAsync(ProductFeedItem item, ContentItem contentItem, ProductPart product, ProductVariant variant, DateTime now, string currency, IShopperCurrency shopperCurrency, CancellationToken cancellationToken)
    {
        var listed = contentItem.TryGet<ProductPricePart>(out var prices) ? prices.GetDefault(now) : null;
        var regular = variant?.Price ?? product.Price ?? (listed is { Kind: PriceKind.OneTime } ? listed.Amount : 0m);
        var sale = ProductSale.GetSalePrice(product, variant, now) ?? (product.SaleStartsUtc > now ? variant?.SalePrice ?? product.SalePrice : null);
        var from = variant?.Price is null && product.Price is null && listed is not null && !string.IsNullOrEmpty(listed.Currency) ? listed.Currency : product.Currency;

        item.Currency = string.IsNullOrEmpty(currency) ? from : currency.ToUpperInvariant();

        if (!string.Equals(item.Currency, from, StringComparison.OrdinalIgnoreCase))
        {
            if (shopperCurrency is null || await shopperCurrency.ConvertAsync(regular, from, item.Currency, isPrice: true, cancellationToken) is not { } converted)
            {
                return false;
            }

            regular = converted;
            sale = sale is { } amount ? await shopperCurrency.ConvertAsync(amount, from, item.Currency, isPrice: true, cancellationToken) : null;
        }

        item.Price = regular;
        item.SalePrice = sale is { } price && price < regular ? price : null;

        if (item.SalePrice is not null && product.SaleStartsUtc is { } starts && product.SaleEndsUtc is { } ends)
        {
            item.SaleStartsUtc = starts;
            item.SaleEndsUtc = ends;
        }

        return true;
    }

    private async Task<string> GetLinkAsync(ContentItem contentItem, string baseUrl)
    {
        if (string.IsNullOrEmpty(baseUrl))
        {
            return null;
        }

        var metadata = await _contentManager.PopulateAspectAsync<ContentItemMetadata>(contentItem);
        var path = metadata.DisplayRouteValues is null ? null : _linkGenerator.GetPathByRouteValues(routeName: null, metadata.DisplayRouteValues, pathBase: string.Empty);

        return path is null ? null : baseUrl + path;
    }

    private static string ToAbsolute(string url, string baseUrl)
    {
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute) && absolute.Scheme.StartsWith("http", StringComparison.Ordinal))
        {
            return url;
        }

        if (string.IsNullOrEmpty(baseUrl))
        {
            return null;
        }

        // A media address is relative to the host, and includes the tenant's prefix.
        var host = new Uri(baseUrl).GetLeftPart(UriPartial.Authority);

        return url.StartsWith('/') ? host + url : baseUrl + "/" + url;
    }

    // Every taxonomy term on the item, with the name of the field it's in.
    private static IEnumerable<(string Field, string TermId)> TermsOf(ContentItem contentItem)
    {
        var found = new List<(string, string)>();

        void Walk(string name, JsonNode node)
        {
            switch (node)
            {
                case JsonObject obj:
                    if (obj["TermContentItemIds"] is JsonArray termIds)
                    {
                        found.AddRange(termIds.Select(id => id?.ToString()).Where(id => !string.IsNullOrEmpty(id)).Select(id => (name, id)));
                    }

                    foreach (var (key, child) in obj)
                    {
                        Walk(key, child);
                    }

                    break;
                case JsonArray array:
                    foreach (var child in array)
                    {
                        Walk(name, child);
                    }

                    break;
            }
        }

        Walk(null, JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(contentItem.Content)));

        return found.Distinct();
    }

    // The text of a field with one of the names in any part, such as a product's Color.
    private static string TextFieldOf(ContentItem contentItem, params string[] names)
    {
        var content = JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(contentItem.Content)) as JsonObject;

        foreach (var (_, part) in content ?? [])
        {
            if (part is not JsonObject fields)
            {
                continue;
            }

            foreach (var (name, field) in fields)
            {
                if (names.Any(candidate => string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase)) &&
                    field?["Text"]?.ToString() is { Length: > 0 } text)
                {
                    return text;
                }
            }
        }

        return null;
    }

    private async Task<Dictionary<string, FeedTerm>> LoadTermsAsync(CancellationToken cancellationToken)
    {
        var terms = new Dictionary<string, FeedTerm>(StringComparer.Ordinal);
        var taxonomies = await _session.Query<ContentItem, ContentItemIndex>(index => index.ContentType == "Taxonomy" && index.Published).ListAsync(cancellationToken);

        void Collect(JsonNode term, string taxonomy, string parentPath)
        {
            if (term?["ContentItemId"]?.ToString() is not { Length: > 0 } id)
            {
                return;
            }

            var name = term["DisplayText"]?.ToString() is { Length: > 0 } text ? text : id;
            var path = string.IsNullOrEmpty(parentPath) ? name : parentPath + " > " + name;

            terms.TryAdd(id, new FeedTerm(taxonomy, name, path));

            foreach (var child in term["Terms"] as JsonArray ?? [])
            {
                Collect(child, taxonomy, path);
            }
        }

        foreach (var taxonomy in taxonomies)
        {
            var root = JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(taxonomy.Content));

            foreach (var term in root?["TaxonomyPart"]?["Terms"] as JsonArray ?? [])
            {
                Collect(term, taxonomy.DisplayText ?? string.Empty, null);
            }
        }

        return terms;
    }

    private static bool Mentions(string name, string word)
        => name?.Contains(word, StringComparison.OrdinalIgnoreCase) == true;

    private static bool IsOption(string name, params string[] names)
        => names.Any(candidate => string.Equals(name?.Trim(), candidate, StringComparison.OrdinalIgnoreCase));

    private static string Truncate(string value, int length)
        => value is null || value.Length <= length ? value : value[..length].TrimEnd();

    private sealed record FeedTerm(string Taxonomy, string Name, string Path);
}
