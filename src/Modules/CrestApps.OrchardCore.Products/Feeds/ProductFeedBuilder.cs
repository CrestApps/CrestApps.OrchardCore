using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Commerce.Stores;
using CrestApps.OrchardCore.Products.Core.Feeds;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Products.Services;
using CrestApps.OrchardCore.Products.Services.Attributes;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
/// An item Merchant Center would refuse is left out, with why. The store profile gives every item its seller and policy
/// links, and each <see cref="IProductFeedItemContributor"/> adds what its module owns, such as stock, weight or returns.
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
    private readonly IShopperCurrency _shopperCurrency;
    private readonly TaxonomyTermLookup _termLookup;
    private readonly IEnumerable<IProductFeedItemContributor> _contributors;
    private readonly ILogger _logger;

    public ProductFeedBuilder(
        ISession session,
        IContentManager contentManager,
        IContentDefinitionManager contentDefinitionManager,
        ProductContentTypes productTypes,
        ISiteService siteService,
        IServiceProvider serviceProvider,
        LinkGenerator linkGenerator,
        IClock clock,
        IShopperCurrency shopperCurrency,
        TaxonomyTermLookup termLookup,
        IEnumerable<IProductFeedItemContributor> contributors,
        ILogger<ProductFeedBuilder> logger)
    {
        _session = session;
        _contentManager = contentManager;
        _contentDefinitionManager = contentDefinitionManager;
        _productTypes = productTypes;
        _siteService = siteService;
        _serviceProvider = serviceProvider;
        _linkGenerator = linkGenerator;
        _clock = clock;
        _shopperCurrency = shopperCurrency;
        _termLookup = termLookup;
        _contributors = contributors;
        _logger = logger;
    }

    /// <summary>
    /// Builds the feed, priced in <paramref name="currency"/>, or the store's when none.
    /// </summary>
    public Task<ProductFeed> BuildAsync(string currency = null, CancellationToken cancellationToken = default)
        => BuildAsync(currency, contentItemIds: null, cancellationToken);

    /// <summary>
    /// Builds the feed of some products, or of all when <paramref name="contentItemIds"/> is <see langword="null"/>,
    /// priced in <paramref name="currency"/>, or the store's when none.
    /// </summary>
    public async Task<ProductFeed> BuildAsync(string currency, IReadOnlyCollection<string> contentItemIds, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var site = await _siteService.GetSiteSettingsAsync();
        var settings = await _siteService.GetSettingsAsync<ProductFeedSettings>();
        var profile = await _siteService.GetStoreProfileAsync();
        var baseUrl = site.BaseUrl?.TrimEnd('/');
        var sellerName = string.IsNullOrWhiteSpace(profile.Name) ? site.SiteName : profile.Name.Trim();
        var sellerUrl = ToSiteUrl(profile.Website, baseUrl) ?? (string.IsNullOrEmpty(baseUrl) ? null : baseUrl + "/");
        var policies = profile.Policies ?? new StorePolicyLinks();
        var termsUrl = ToSiteUrl(policies.TermsOfService, baseUrl);
        var privacyUrl = ToSiteUrl(policies.PrivacyPolicy, baseUrl);
        var returnsUrl = ToSiteUrl(policies.ReturnPolicy, baseUrl);
        var types = await _productTypes.GetProductTypesAsync(_contentDefinitionManager);
        var availabilityProviders = _serviceProvider.GetServices<IProductAvailabilityProvider>();
        var media = _serviceProvider.GetService<IMediaFileStore>();

        var items = new List<ProductFeedItem>();
        var problems = new List<ProductFeedProblem>();

        if (types.Count == 0 || contentItemIds is { Count: 0 })
        {
            return new ProductFeed(items, problems, now);
        }

        for (var skip = 0; ; skip += BatchSize)
        {
            var query = contentItemIds is null
                ? _session.Query<ContentItem, ContentItemIndex>(index => index.ContentType.IsIn(types) && index.Published)
                : _session.Query<ContentItem, ContentItemIndex>(index => index.ContentType.IsIn(types) && index.Published && index.ContentItemId.IsIn(contentItemIds));

            var batch = (await query
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
                var fieldTerms = await GetFieldTermsAsync(contentItem);

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
                        Id = id,
                        ItemGroupId = variant is null ? null : contentItem.ContentItemId,
                        ContentItemId = contentItem.ContentItemId,
                        VariantId = variant?.VariantId,
                        Title = Truncate(title, ProductFeedValidator.MaxTitleLength),
                        ProductTitle = Truncate(contentItem.DisplayText, ProductFeedValidator.MaxTitleLength),
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
                        VariantOptions = variant?.Options is { Count: > 0 } options
                            ? new Dictionary<string, string>(options, StringComparer.OrdinalIgnoreCase)
                            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                        AvailableQuantity = itemStock is null ? null : Math.Max(0, itemStock.Available),
                        SellerName = sellerName,
                        SellerUrl = sellerUrl,
                        TermsOfServiceUrl = termsUrl,
                        PrivacyPolicyUrl = privacyUrl,
                        ReturnPolicyUrl = returnsUrl,
                        IsDigital = partSettings.Type == ProductType.Digital,
                        ShowToAgents = feedPart?.ShowToAgents ?? settings.ShowToAgentsByDefault,
                        AgentCheckout = feedPart?.AgentCheckout ?? settings.AgentCheckoutByDefault,
                    };

                    // Checking out needs search: an agent can't buy what it isn't shown.
                    item.AgentCheckout &= item.ShowToAgents;

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
                    candidates.Add((Item(ProductFeedItemIds.Create(contentItem.ContentItemId, null, product.Sku), contentItem.DisplayText, null, stock), null));
                }
                else
                {
                    var optionNames = variants.Options.Select(option => option.Name).ToList();
                    var variantStock = await availabilityProviders.GetAvailabilityAsync([.. enabled.Select(variant => new ProductStockKey(contentItem.ContentItemId, variant.VariantId))], cancellationToken: cancellationToken);

                    foreach (var variant in enabled)
                    {
                        var variantTitle = variant.GetTitle(optionNames);
                        var title = string.IsNullOrEmpty(variantTitle) ? contentItem.DisplayText : $"{contentItem.DisplayText} - {variantTitle}";

                        candidates.Add((Item(ProductFeedItemIds.Create(contentItem.ContentItemId, variant.VariantId, variant.Sku), title, variant, variantStock.GetValueOrDefault(new ProductStockKey(contentItem.ContentItemId, variant.VariantId)) ?? stock), variant));
                    }
                }

                foreach (var (item, variant) in candidates)
                {
                    if (!settings.IncludeOutOfStock && item.Availability == "out_of_stock")
                    {
                        continue;
                    }

                    if (!await PriceAsync(item, contentItem, product, variant, now, currency, _shopperCurrency, cancellationToken))
                    {
                        problems.Add(new ProductFeedProblem(item.Id, item.Title, $"it can't be priced in {currency}", LeftOut: true));

                        continue;
                    }

                    await ContributeAsync(new ProductFeedItemContext(contentItem, variant, partSettings.Type, item), cancellationToken);

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
            if (await shopperCurrency.ConvertAsync(regular, from, item.Currency, isPrice: true, cancellationToken) is not { } converted)
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

    // Each contributor adds what its module owns; one that fails leaves its values out, not the item.
    private async Task ContributeAsync(ProductFeedItemContext context, CancellationToken cancellationToken)
    {
        foreach (var contributor in _contributors)
        {
            try
            {
                await contributor.ContributeAsync(context, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(exception, "The product feed contributor '{Contributor}' failed for the item '{ItemId}'.", contributor.GetType().Name, context.Item.Id);
            }
        }
    }

    // A link the store set, such as a policy: an absolute address as it is, or a path on the site after its base URL.
    internal static string ToSiteUrl(string url, string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        url = url.Trim();

        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute) && (absolute.Scheme == Uri.UriSchemeHttps || absolute.Scheme == Uri.UriSchemeHttp))
        {
            return url;
        }

        return string.IsNullOrEmpty(baseUrl) || url.Contains(':', StringComparison.Ordinal)
            ? null
            : baseUrl + "/" + url.TrimStart('~').TrimStart('/');
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

    // Every taxonomy term on the item, with the name of the field it's in, its taxonomy's name and its path.
    private async Task<List<(string Field, FeedTerm Term)>> GetFieldTermsAsync(ContentItem contentItem)
    {
        var fieldTerms = new List<(string Field, FeedTerm Term)>();

        foreach (var (field, taxonomyId, termId) in TermsOf(contentItem))
        {
            var path = await _termLookup.GetPathAsync(taxonomyId, termId);

            if (path.Count == 0)
            {
                continue;
            }

            var taxonomy = await _termLookup.GetTaxonomyNameAsync(taxonomyId) ?? string.Empty;

            fieldTerms.Add((field, new FeedTerm(taxonomy, path[path.Count - 1].DisplayText, string.Join(" > ", path.Select(term => term.DisplayText)))));
        }

        return fieldTerms;
    }

    // Every taxonomy term on the item, with the name of the field it's in and the taxonomy the field picks from.
    private static IEnumerable<(string Field, string TaxonomyId, string TermId)> TermsOf(ContentItem contentItem)
    {
        var found = new List<(string, string, string)>();

        void Walk(string name, JsonNode node)
        {
            switch (node)
            {
                case JsonObject obj:
                    if (obj["TermContentItemIds"] is JsonArray termIds)
                    {
                        var taxonomyId = obj["TaxonomyContentItemId"]?.ToString();

                        found.AddRange(termIds.Select(id => id?.ToString()).Where(id => !string.IsNullOrEmpty(id)).Select(id => (name, taxonomyId, id)));
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

    private static bool Mentions(string name, string word)
        => name?.Contains(word, StringComparison.OrdinalIgnoreCase) == true;

    private static bool IsOption(string name, params string[] names)
        => names.Any(candidate => string.Equals(name?.Trim(), candidate, StringComparison.OrdinalIgnoreCase));

    private static string Truncate(string value, int length)
        => value is null || value.Length <= length ? value : value[..length].TrimEnd();

    private sealed record FeedTerm(string Taxonomy, string Name, string Path);
}
