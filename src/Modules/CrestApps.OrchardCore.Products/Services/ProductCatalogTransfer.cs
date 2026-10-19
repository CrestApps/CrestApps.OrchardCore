using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Products.Core.Transfer;
using CrestApps.OrchardCore.Products.Services.Attributes;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentManagement.Records;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Products.Services;

/// <summary>
/// Imports a product export (Shopify's or WooCommerce's) into a product content type, and exports a type's products
/// as Shopify's format. A product already in the store, matched by its SKU or one of its variants', is updated.
/// </summary>
public sealed class ProductCatalogTransfer
{
    /// <summary>
    /// The most products one import takes, so it finishes within a request.
    /// </summary>
    public const int MaxProducts = 2000;

    private const int BatchSize = 200;

    private readonly IContentManager _contentManager;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ISession _session;
    private readonly TaxonomyTermLookup _terms;
    private readonly IEnumerable<IProductImportHandler> _handlers;
    private readonly IEnumerable<IProductAvailabilityProvider> _availability;

    internal readonly IStringLocalizer S;

    public ProductCatalogTransfer(
        IContentManager contentManager,
        IContentDefinitionManager contentDefinitionManager,
        ISession session,
        TaxonomyTermLookup terms,
        IEnumerable<IProductImportHandler> handlers,
        IEnumerable<IProductAvailabilityProvider> availability,
        IStringLocalizer<ProductCatalogTransfer> stringLocalizer)
    {
        _contentManager = contentManager;
        _contentDefinitionManager = contentDefinitionManager;
        _session = session;
        _terms = terms;
        _handlers = handlers;
        _availability = availability;
        S = stringLocalizer;
    }

    public async Task<ProductImportResult> ImportAsync(IReadOnlyList<ImportedProduct> products, ProductImportOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var result = new ProductImportResult();
        var type = await _contentDefinitionManager.GetTypeDefinitionAsync(options.ContentType);

        if (type is null || !type.Parts.Any(part => part.PartDefinition.Name == nameof(ProductPart)))
        {
            result.Problems.Add(S["Choose a product type to import into."]);

            return result;
        }

        if ((products?.Count ?? 0) > MaxProducts)
        {
            result.Problems.Add(S["The file has {0} products; an import takes up to {1}. Split it, then import each part.", products.Count, MaxProducts]);

            return result;
        }

        var map = await MapTypeAsync(type);
        var existing = await FindBySkuAsync(type.Name, cancellationToken);
        var saved = new List<ProductImportEntry>();
        var missingTerms = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var product in products ?? [])
        {
            // By SKU, or for a product without one, by its handle: the last part of its address, which the export
            // writes.
            var match = new[] { product.Sku }.Concat(product.Variants.Select(variant => variant.Sku))
                .Where(sku => !string.IsNullOrEmpty(sku))
                .Select(sku => existing.Skus.GetValueOrDefault(sku))
                .FirstOrDefault(id => id is not null)
                ?? (string.IsNullOrEmpty(product.Key) ? null : existing.Handles.GetValueOrDefault(product.Key));

            if (match is not null && !options.UpdateExisting)
            {
                result.Skipped++;

                continue;
            }

            var item = match is null
                ? await _contentManager.NewAsync(type.Name)
                : await _contentManager.GetAsync(match, VersionOptions.DraftRequired);

            if (item is null)
            {
                continue;
            }

            var wasPublished = match is not null && await _contentManager.GetAsync(match, VersionOptions.Published) is not null;

            Apply(item, product, map, result, missingTerms);

            if (match is null)
            {
                await _contentManager.CreateAsync(item, options.Publish && product.Published ? VersionOptions.Published : VersionOptions.Draft);
                result.Created++;
            }
            else
            {
                await _contentManager.UpdateAsync(item);

                if (wasPublished || (options.Publish && product.Published))
                {
                    await _contentManager.PublishAsync(item);
                }

                result.Updated++;
            }

            foreach (var sku in new[] { product.Sku }.Concat(product.Variants.Select(variant => variant.Sku)).Where(sku => !string.IsNullOrEmpty(sku)))
            {
                existing.Skus[sku] = item.ContentItemId;
            }

            result.ImagesNotImported += product.ImageUrls.Count;
            saved.Add(new ProductImportEntry(item, product));
        }

        foreach (var name in missingTerms)
        {
            result.Problems.Add(S["'{0}' isn't a term of its taxonomy yet, so products filed under it were left without it. Add the term, then import again.", name]);
        }

        if (saved.Count > 0)
        {
            foreach (var handler in _handlers)
            {
                await handler.ImportedAsync(saved, cancellationToken);
            }
        }

        return result;
    }

    /// <summary>
    /// The type's products as Shopify's product export.
    /// </summary>
    public async Task<string> ExportAsync(string contentType, CancellationToken cancellationToken = default)
    {
        var type = await _contentDefinitionManager.GetTypeDefinitionAsync(contentType);

        if (type is null)
        {
            return ProductCsv.WriteShopify([]);
        }

        var map = await MapTypeAsync(type);
        var products = new List<ImportedProduct>();

        for (var skip = 0; ; skip += BatchSize)
        {
            var items = (await _session.Query<ContentItem, ContentItemIndex>(index => index.ContentType == contentType && index.Latest)
                .OrderBy(index => index.Id)
                .Skip(skip)
                .Take(BatchSize)
                .ListAsync(cancellationToken)).ToList();

            if (items.Count == 0)
            {
                break;
            }

            var keys = items.SelectMany(item => item.TryGet<ProductVariantsPart>(out var variants) && variants.HasVariants
                    ? variants.Variants.Select(variant => new ProductStockKey(item.ContentItemId, variant.VariantId))
                    : [new ProductStockKey(item.ContentItemId, null)])
                .ToArray();
            var stock = await _availability.GetAvailabilityAsync(keys);

            foreach (var item in items)
            {
                products.Add(Describe(item, map, stock));
            }

            if (items.Count < BatchSize)
            {
                break;
            }
        }

        return ProductCsv.WriteShopify(products);
    }

    private static ImportedProduct Describe(ContentItem item, TypeMap map, IReadOnlyDictionary<ProductStockKey, ProductAvailability> stock)
    {
        var content = (JsonObject)item.Content;
        var part = item.TryGet<ProductPart>(out var found) ? found : new ProductPart();
        var product = new ImportedProduct
        {
            Key = Handle(item),
            Title = item.DisplayText,
            DescriptionHtml = content["HtmlBodyPart"]?["Html"]?.GetValue<string>(),
            Sku = part.Sku,
            Price = part.Price,
            CompareAtPrice = part.CompareAtPrice,
            Published = item.Published,
            Quantity = stock.GetValueOrDefault(new ProductStockKey(item.ContentItemId, null))?.Available,
            Category = TermName(content, map.Category),
            Brand = TermName(content, map.Brand),
        };

        if (item.TryGet<ProductVariantsPart>(out var variants) && variants.HasVariants)
        {
            product.Options = [.. variants.Options];
            product.Variants = [.. variants.Variants.Where(variant => variant.Enabled).Select(variant => new ImportedVariant
            {
                Options = new Dictionary<string, string>(variant.Options, StringComparer.OrdinalIgnoreCase),
                Sku = variant.Sku,
                Price = variant.Price ?? part.Price,
                CompareAtPrice = variant.CompareAtPrice,
                Quantity = stock.GetValueOrDefault(new ProductStockKey(item.ContentItemId, variant.VariantId))?.Available,
            })];
        }

        return product;
    }

    private void Apply(ContentItem item, ImportedProduct product, TypeMap map, ProductImportResult result, ISet<string> missingTerms)
    {
        var content = (JsonObject)item.Content;

        item.DisplayText = product.Title;

        if (map.HasTitle)
        {
            Set(content, "TitlePart", "Title", product.Title);
        }

        if (map.HasHtmlBody && product.DescriptionHtml is not null)
        {
            Set(content, "HtmlBodyPart", "Html", product.DescriptionHtml);
        }

        var options = product.Options;
        var unknown = options.Select(option => option.Name).FirstOrDefault(name => !map.OptionNames.Contains(name, StringComparer.OrdinalIgnoreCase));

        // Variants need the type to offer them, with options of the same names.
        if (options.Count > 0 && (!map.HasVariants || unknown is not null))
        {
            result.Problems.Add(map.HasVariants
                ? S["Row {0}: {1} has a '{2}' option, which the {3} type doesn't offer. It was imported without its variants; add the option to the type's Product Variants settings, then import again.", product.Row, product.Title, unknown, map.DisplayName]
                : S["Row {0}: {1} has variants, but the {2} type has no Product Variants part. It was imported without them; add the part, then import again.", product.Row, product.Title, map.DisplayName]);
            options = [];

            // Sold as one product: from its lowest price, with the stock of all its variants.
            product.Price = product.Variants.Select(variant => variant.Price).Where(price => price.HasValue).DefaultIfEmpty(product.Price).Min();
            product.CompareAtPrice ??= product.Variants.Select(variant => variant.CompareAtPrice).FirstOrDefault(price => price.HasValue);
            product.Quantity ??= product.Variants.Any(variant => variant.Quantity.HasValue) ? product.Variants.Sum(variant => variant.Quantity ?? 0) : null;
            product.Variants.Clear();
        }

        item.Alter<ProductPart>(part =>
        {
            part.Price = product.Price ?? part.Price;
            part.CompareAtPrice = product.CompareAtPrice;
            part.Sku = product.Sku ?? (options.Count == 0 ? product.Variants.FirstOrDefault()?.Sku : null) ?? part.Sku;

            if (product.ExternalUrl is not null)
            {
                part.ExternalUrl = product.ExternalUrl;
                part.ExternalButtonText = product.ExternalButtonText;
            }
        });

        if (options.Count > 0)
        {
            item.Alter<ProductVariantsPart>(part =>
            {
                // The type's spelling of each option.
                part.Options = [.. options.Select(option => new ProductOption
                {
                    Name = map.OptionNames.First(name => string.Equals(name, option.Name, StringComparison.OrdinalIgnoreCase)),
                    Values = [.. option.Values],
                })];
                part.Variants = ProductVariants.Combine(part.Options, part.Variants, UniqueId.GenerateId);

                // A combination the file doesn't sell is switched off.
                foreach (var variant in part.Variants)
                {
                    var imported = product.Variants.FirstOrDefault(entry => entry.Options.Count == variant.Options.Count &&
                        entry.Options.All(pair => variant.Options.TryGetValue(pair.Key, out var value) && string.Equals(value, pair.Value, StringComparison.OrdinalIgnoreCase)));

                    variant.Enabled = imported is not null;

                    if (imported is not null)
                    {
                        variant.Sku = imported.Sku ?? variant.Sku;
                        variant.Price = imported.Price;
                        variant.CompareAtPrice = imported.CompareAtPrice;
                        imported.VariantId = variant.VariantId;
                    }
                }
            });
        }

        SetTerm(content, map.Category, product.Category, missingTerms);
        SetTerm(content, map.Brand, product.Brand, missingTerms);
    }

    private static void SetTerm(JsonObject content, TaxonomyMap taxonomy, string name, ISet<string> missingTerms)
    {
        if (taxonomy is null || string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var term = taxonomy.Terms.Values.FirstOrDefault(entry => string.Equals(entry.DisplayText, name, StringComparison.OrdinalIgnoreCase));

        if (term is null)
        {
            missingTerms.Add(name);

            return;
        }

        if (content[taxonomy.PartName] is not JsonObject part)
        {
            part = [];
            content[taxonomy.PartName] = part;
        }

        part[taxonomy.FieldName] = new JsonObject
        {
            ["TaxonomyContentItemId"] = taxonomy.TaxonomyId,
            ["TermContentItemIds"] = new JsonArray(term.ContentItemId),
        };
    }

    private static string TermName(JsonObject content, TaxonomyMap taxonomy)
    {
        if (taxonomy is null || content[taxonomy.PartName]?[taxonomy.FieldName]?["TermContentItemIds"] is not JsonArray ids || ids.Count == 0)
        {
            return null;
        }

        var id = ids[0]?.GetValue<string>();

        return id is not null && taxonomy.Terms.TryGetValue(id, out var term) ? term.DisplayText : null;
    }

    // The last part of the product's address, such as "classic-tee" for "shop/classic-tee".
    private static string Handle(ContentItem item)
        => ((JsonObject)item.Content)["AutoroutePart"]?["Path"]?.GetValue<string>() is { Length: > 0 } path ? path.TrimEnd('/').Split('/')[^1] : null;

    private static void Set(JsonObject content, string partName, string property, string value)
    {
        if (content[partName] is not JsonObject part)
        {
            part = [];
            content[partName] = part;
        }

        part[property] = value;
    }

    // Every product of the type by its SKU and its variants' SKUs.
    private async Task<(Dictionary<string, string> Skus, Dictionary<string, string> Handles)> FindBySkuAsync(string contentType, CancellationToken cancellationToken)
    {
        var skus = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var handles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var skip = 0; ; skip += BatchSize)
        {
            var items = (await _session.Query<ContentItem, ContentItemIndex>(index => index.ContentType == contentType && index.Latest)
                .OrderBy(index => index.Id)
                .Skip(skip)
                .Take(BatchSize)
                .ListAsync(cancellationToken)).ToList();

            foreach (var item in items)
            {
                if (item.TryGet<ProductPart>(out var productPart) && productPart.Sku is { Length: > 0 } sku)
                {
                    skus.TryAdd(sku, item.ContentItemId);
                }

                if (item.TryGet<ProductVariantsPart>(out var variants))
                {
                    foreach (var variant in variants.Variants.Where(variant => !string.IsNullOrEmpty(variant.Sku)))
                    {
                        skus.TryAdd(variant.Sku, item.ContentItemId);
                    }
                }

                if (Handle(item) is { } handle)
                {
                    handles.TryAdd(handle, item.ContentItemId);
                }
            }

            if (items.Count < BatchSize)
            {
                return (skus, handles);
            }
        }
    }

    // What the type holds: its title, description and variants, and which taxonomy fields are its category and brand.
    private async Task<TypeMap> MapTypeAsync(ContentTypeDefinition type)
    {
        var parts = type.Parts.ToList();
        var variantsPart = parts.FirstOrDefault(part => part.PartDefinition.Name == nameof(ProductVariantsPart));
        var taxonomyFields = parts
            .SelectMany(part => part.PartDefinition.Fields.Select(field => (Part: part, Field: field)))
            .Where(entry => entry.Field.FieldDefinition.Name == "TaxonomyField")
            .ToList();

        return new TypeMap
        {
            DisplayName = type.DisplayName,
            HasTitle = parts.Any(part => part.PartDefinition.Name == "TitlePart"),
            HasHtmlBody = parts.Any(part => part.PartDefinition.Name == "HtmlBodyPart"),
            HasVariants = variantsPart is not null,
            OptionNames = [.. variantsPart?.GetSettings<ProductVariantsPartSettings>()?.Options?.Select(option => option.Name).Where(name => !string.IsNullOrWhiteSpace(name)) ?? []],
            Category = await MapTaxonomyAsync(taxonomyFields, "categor", "type", "collection"),
            Brand = await MapTaxonomyAsync(taxonomyFields, "brand", "vendor", "manufacturer"),
        };
    }

    // The first taxonomy field whose name or display name says what it is, such as Category or Brand.
    private async Task<TaxonomyMap> MapTaxonomyAsync(List<(ContentTypePartDefinition Part, ContentPartFieldDefinition Field)> fields, params string[] words)
    {
        var (part, field) = fields.FirstOrDefault(entry => words.Any(word =>
            entry.Field.Name.Contains(word, StringComparison.OrdinalIgnoreCase) ||
            (entry.Field.DisplayName() ?? string.Empty).Contains(word, StringComparison.OrdinalIgnoreCase)));

        if (field is null || FieldValueReader.GetSettingString(field, "TaxonomyFieldSettings", "TaxonomyContentItemId") is not { Length: > 0 } taxonomyId)
        {
            return null;
        }

        return new TaxonomyMap(part.Name, field.Name, taxonomyId, await _terms.GetTermsAsync(taxonomyId));
    }

    private sealed class TypeMap
    {
        public string DisplayName { get; init; }

        public bool HasTitle { get; init; }

        public bool HasHtmlBody { get; init; }

        public bool HasVariants { get; init; }

        public List<string> OptionNames { get; init; } = [];

        public TaxonomyMap Category { get; init; }

        public TaxonomyMap Brand { get; init; }
    }

    private sealed record TaxonomyMap(string PartName, string FieldName, string TaxonomyId, IReadOnlyDictionary<string, TaxonomyTerm> Terms);
}

/// <summary>
/// How an import treats the products it reads.
/// </summary>
public sealed class ProductImportOptions
{
    /// <summary>
    /// Gets or sets the product type new products are made as.
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// Gets or sets whether a product already in the store, matched by SKU, is updated, or left as it is.
    /// </summary>
    public bool UpdateExisting { get; set; } = true;

    /// <summary>
    /// Gets or sets whether new products the file says are published are published, or kept as drafts to review.
    /// </summary>
    public bool Publish { get; set; }
}

/// <summary>
/// What an import did.
/// </summary>
public sealed class ProductImportResult
{
    public int Created { get; set; }

    public int Updated { get; set; }

    public int Skipped { get; set; }

    /// <summary>
    /// Gets or sets how many image addresses the file had. Images aren't downloaded; they're added in the media library.
    /// </summary>
    public int ImagesNotImported { get; set; }

    public List<string> Problems { get; } = [];
}
