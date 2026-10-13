using System.Globalization;
using System.Text;
using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Indexes;
using CrestApps.OrchardCore.Products.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Contents;
using OrchardCore.DisplayManagement;
using OrchardCore.Media;
using YesSql;
using ISession = YesSql.ISession;
using YesSql.Services;

namespace CrestApps.OrchardCore.Products.Services;

/// <summary>
/// Builds a product listing page: resolves its filters and sorts, runs the search, and turns the results into
/// shapes a theme can override.
/// </summary>
public sealed class ProductListingService
{
    private readonly ProductAttributeCatalog _attributeCatalog;
    private readonly IProductSearchService _searchService;
    private readonly ISession _session;
    private readonly IContentManager _contentManager;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IShapeFactory _shapeFactory;
    private readonly IMediaFileStore _mediaFileStore;
    private readonly IAuthorizationService _authorizationService;

    internal readonly IStringLocalizer S;

    public ProductListingService(
        ProductAttributeCatalog attributeCatalog,
        IProductSearchService searchService,
        ISession session,
        IContentManager contentManager,
        IContentDefinitionManager contentDefinitionManager,
        IShapeFactory shapeFactory,
        IMediaFileStore mediaFileStore,
        IAuthorizationService authorizationService,
        IStringLocalizer<ProductListingService> stringLocalizer)
    {
        _attributeCatalog = attributeCatalog;
        _searchService = searchService;
        _session = session;
        _contentManager = contentManager;
        _contentDefinitionManager = contentDefinitionManager;
        _shapeFactory = shapeFactory;
        _mediaFileStore = mediaFileStore;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    /// <summary>
    /// The filters a listing offers: its configured ones, or one per filterable attribute when it is automatic.
    /// </summary>
    public async Task<IReadOnlyList<ProductListingFacet>> GetFacetsAsync(ProductListingPart part, IReadOnlyList<ProductAttributeDescriptor> attributes)
    {
        if (!part.AutomaticFacets)
        {
            return (part.Facets ?? [])
                .Where(facet => !string.IsNullOrEmpty(facet.Key) && !string.IsNullOrEmpty(facet.AttributeKey))
                .Select(facet => Resolve(facet, attributes))
                .ToArray();
        }

        var facets = new List<ProductListingFacet>();
        var types = await GetContentTypesAsync(part);

        if (types.Count > 1)
        {
            facets.Add(new ProductListingFacet
            {
                Key = "type",
                AttributeKey = ProductAttributeKeys.ContentType,
                Label = S["Type"],
                Kind = ProductFacetKind.Checkbox,
            });
        }

        foreach (var attribute in attributes.Where(attribute => attribute.Settings.Filterable))
        {
            var kind = attribute.Kind switch
            {
                ProductAttributeValueKind.Number => ProductFacetKind.Range,
                ProductAttributeValueKind.Boolean => ProductFacetKind.Toggle,
                ProductAttributeValueKind.Date => (ProductFacetKind?)null,
                _ => ProductFacetKind.Checkbox,
            };

            if (kind is null)
            {
                continue;
            }

            facets.Add(new ProductListingFacet
            {
                Key = ToUrlKey(attribute.Key),
                AttributeKey = attribute.Key,
                Label = attribute.Label,
                Kind = kind.Value,
            });
        }

        facets.Add(new ProductListingFacet
        {
            Key = "price",
            AttributeKey = ProductAttributeKeys.Price,
            Label = S["Price"],
            Kind = ProductFacetKind.Range,
        });

        return facets;
    }

    /// <summary>
    /// The sorts a listing offers: its configured ones, or the standard ones plus every sortable attribute.
    /// </summary>
    public IReadOnlyList<ProductListingSort> GetSorts(ProductListingPart part, IReadOnlyList<ProductAttributeDescriptor> attributes)
    {
        if (!part.AutomaticSorts && part.Sorts is { Count: > 0 })
        {
            return part.Sorts.Where(sort => !string.IsNullOrEmpty(sort.Key)).ToArray();
        }

        var sorts = new List<ProductListingSort>
        {
            new() { Key = "newest", Label = S["Newest"], AttributeKey = ProductAttributeKeys.Published, Descending = true },
            new() { Key = "price-asc", Label = S["Price: low to high"], AttributeKey = ProductAttributeKeys.Price },
            new() { Key = "price-desc", Label = S["Price: high to low"], AttributeKey = ProductAttributeKeys.Price, Descending = true },
            new() { Key = "name", Label = S["Name: A to Z"], AttributeKey = ProductAttributeKeys.Title },
        };

        foreach (var attribute in attributes.Where(attribute =>
            attribute.Settings.Sortable && attribute.Kind is ProductAttributeValueKind.Number or ProductAttributeValueKind.Date))
        {
            var key = ToUrlKey(attribute.Key);

            sorts.Add(new ProductListingSort { Key = key + "-asc", Label = S["{0}: low to high", attribute.Label], AttributeKey = attribute.Key });
            sorts.Add(new ProductListingSort { Key = key + "-desc", Label = S["{0}: high to low", attribute.Label], AttributeKey = attribute.Key, Descending = true });
        }

        return sorts;
    }

    /// <summary>
    /// The product types a listing shows that the current visitor may view.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetContentTypesAsync(ProductListingPart part, HttpContext httpContext = null)
    {
        var productTypes = await _attributeCatalog.GetProductTypesAsync();
        var requested = part.ContentTypes?.Where(type => !string.IsNullOrEmpty(type)).ToArray() ?? [];

        var types = productTypes
            .Where(type => requested.Length == 0 || requested.Contains(type.Name, StringComparer.Ordinal))
            .Select(type => type.Name)
            .ToList();

        if (httpContext is null)
        {
            return types;
        }

        // Checked once per type rather than per product: a listing must never show a type the visitor may not view.
        var visible = new List<string>();

        foreach (var type in types)
        {
            if (await _authorizationService.AuthorizeAsync(httpContext.User, CommonPermissions.ViewContent, new ContentItem { ContentType = type }))
            {
                visible.Add(type);
            }
        }

        return visible;
    }

    /// <summary>
    /// Fills the listing page model from the request.
    /// </summary>
    public async Task BuildAsync(ProductListingViewModel model, ProductListingPart part, HttpContext httpContext)
    {
        var request = httpContext.Request;
        var types = await GetContentTypesAsync(part, httpContext);
        var attributes = await _attributeCatalog.GetAttributesAsync(types);
        var facets = await GetFacetsAsync(part, attributes);
        var sorts = GetSorts(part, attributes);
        var pageSizes = (part.PageSizes is { Length: > 0 } ? part.PageSizes : [12, 24, 48])
            .Where(size => size is > 0 and <= 200)
            .Distinct()
            .Order()
            .ToArray();

        var state = ProductListingState.Parse(request.Query, facets, sorts.Select(sort => sort.Key), pageSizes);

        var defaultSort = sorts.FirstOrDefault(sort => sort.Key == part.DefaultSort) ?? (sorts.Count > 0 ? sorts[0] : null);
        var sortOption = sorts.FirstOrDefault(sort => sort.Key == state.Sort) ?? defaultSort;
        var defaultPageSize = pageSizes.Contains(part.DefaultPageSize) ? part.DefaultPageSize : pageSizes.FirstOrDefault(24);
        var pageSize = state.PageSize ?? defaultPageSize;
        var view = part.AllowViewSwitching ? state.View ?? part.DefaultView : part.DefaultView;

        var query = new ProductSearchQuery
        {
            ContentTypes = types.ToList(),
            Keyword = part.EnableKeywordSearch ? state.Keyword : null,
            Sort = sortOption,
            Skip = (state.Page - 1) * pageSize,
            Take = pageSize,
        };

        foreach (var facet in facets)
        {
            if (facet.Kind == ProductFacetKind.Range)
            {
                if (state.Ranges.TryGetValue(facet.Key, out var range))
                {
                    query.Filters.Add(new ProductSearchFilter { FacetKey = facet.Key, AttributeKey = facet.AttributeKey, Min = range.Min, Max = range.Max });
                }
            }
            else if (state.Values.TryGetValue(facet.Key, out var values))
            {
                query.Filters.Add(new ProductSearchFilter { FacetKey = facet.Key, AttributeKey = facet.AttributeKey, Values = values });
            }

            query.Facets.Add(new ProductFacetRequest
            {
                Key = facet.Key,
                AttributeKey = facet.AttributeKey,
                Level = facet.Level,
                IsRange = facet.Kind == ProductFacetKind.Range,
            });
        }

        var result = types.Count == 0
            ? new ProductSearchResult()
            : await _searchService.SearchAsync(query);

        var baseUrl = request.PathBase + request.Path;
        string UrlFor(ProductListingState target) => baseUrl + target.ToQueryString();

        model.FormAction = baseUrl;
        model.EnableKeywordSearch = part.EnableKeywordSearch;
        model.Keyword = state.Keyword;
        model.Sort = sortOption?.Key;
        model.DefaultSort = defaultSort?.Key;
        model.Sorts = sorts.Select(sort => new ProductSortOptionViewModel { Key = sort.Key, Label = sort.Label, Selected = sort == sortOption }).ToList();
        model.View = view;
        model.DefaultView = part.DefaultView;
        model.AllowViewSwitching = part.AllowViewSwitching;
        model.GridUrl = UrlFor(state.WithView(ProductListingView.Grid));
        model.ListUrl = UrlFor(state.WithView(ProductListingView.List));
        model.PageSizes = pageSizes;
        model.PageSize = pageSize;
        model.DefaultPageSize = defaultPageSize;
        model.Page = state.Page;
        model.Total = result.Total;
        model.TotalPages = Math.Max(1, (int)Math.Ceiling(result.Total / (double)pageSize));
        model.ClearAllUrl = UrlFor(state.ClearFilters());

        var currency = await GetListingCurrencyAsync(types);

        foreach (var facet in facets)
        {
            var facetModel = BuildFacet(facet, attributes.FirstOrDefault(attribute => attribute.Key == facet.AttributeKey), state, result, currency, UrlFor);

            if (facetModel is null)
            {
                continue;
            }

            // A dependent filter (models under makes) waits for its parent to have a selection.
            if (!string.IsNullOrEmpty(facet.DependsOn) &&
                !state.Values.ContainsKey(facet.DependsOn) &&
                !state.Ranges.ContainsKey(facet.DependsOn))
            {
                continue;
            }

            foreach (var option in facetModel.Options.Where(option => option.Selected))
            {
                model.ActiveFilters.Add(new ProductActiveFilterViewModel
                {
                    Label = facet.Kind == ProductFacetKind.Toggle ? facetModel.Label : $"{facetModel.Label}: {option.Label}",
                    RemoveUrl = option.Url,
                });
            }

            if (facet.Kind == ProductFacetKind.Range && facetModel.HasSelection)
            {
                model.ActiveFilters.Add(new ProductActiveFilterViewModel
                {
                    Label = $"{facetModel.Label}: {FormatRange(facetModel)}",
                    RemoveUrl = facetModel.ClearUrl,
                });
            }

            var shape = await _shapeFactory.CreateAsync<ProductFacetViewModel>("ProductFacet", target => Copy(facetModel, target));
            shape.Metadata.Alternates.Add($"ProductFacet__{facet.Kind}");
            shape.Metadata.Alternates.Add($"ProductFacet__{SafeAlternate(facet.Key)}");
            model.Facets.Add(shape);
        }

        if (!string.IsNullOrEmpty(state.Keyword) && part.EnableKeywordSearch)
        {
            var withoutKeyword = state.Clone();
            withoutKeyword.Keyword = null;
            withoutKeyword.Page = 1;

            model.ActiveFilters.Insert(0, new ProductActiveFilterViewModel
            {
                Label = S["\"{0}\"", state.Keyword],
                RemoveUrl = UrlFor(withoutKeyword),
            });
        }

        await BuildProductsAsync(model, result.ContentItemIds, attributes, view);
        BuildPager(model, state, UrlFor);
    }

    /// <summary>
    /// Builds the specification rows of a product from its attribute values.
    /// </summary>
    public IList<ProductSpecificationViewModel> BuildSpecifications(
        ProductSearchDocument document,
        IReadOnlyList<ProductAttributeDescriptor> attributes,
        Func<ProductAttributeSettings, bool> include)
    {
        var rows = new List<ProductSpecificationViewModel>();

        foreach (var attribute in attributes.Where(attribute => include(attribute.Settings)))
        {
            var values = document.Attributes
                .Where(value => value.Key == attribute.Key)
                .OrderBy(value => value.Level)
                .ThenBy(value => value.Order)
                .Select(value => FormatValue(value, attribute))
                .Where(text => !string.IsNullOrEmpty(text))
                .ToArray();

            if (values.Length > 0)
            {
                rows.Add(new ProductSpecificationViewModel
                {
                    Key = attribute.Key,
                    Label = attribute.Label,
                    Value = string.Join(attribute.Kind == ProductAttributeValueKind.Term ? " › " : ", ", values),
                });
            }
        }

        return rows;
    }

    /// <summary>
    /// Builds the price shape of a product.
    /// </summary>
    public async Task<IShape> BuildPriceShapeAsync(ProductSearchDocument document, string contentType)
    {
        var settings = ProductAttributeCatalog.GetProductSettings(await _contentDefinitionManager.GetTypeDefinitionAsync(contentType));

        return await _shapeFactory.CreateAsync<ProductPriceDisplayViewModel>("ProductPrice", model =>
        {
            model.Display = document.PriceDisplay;
            model.OnRequestLabel = string.IsNullOrWhiteSpace(settings?.OnRequestLabel) ? S["Call for price"] : settings.OnRequestLabel;

            if (document.PriceDisplay != ProductPriceDisplay.Show || !document.Price.HasValue)
            {
                return;
            }

            model.Price = ProductPriceFormatter.Format(document.Price.Value, document.Currency);

            if (document.CompareAtPrice > document.Price && document.CompareAtPrice > 0)
            {
                model.CompareAtPrice = ProductPriceFormatter.Format(document.CompareAtPrice.Value, document.Currency);
                model.DiscountPercent = (int)Math.Round((1 - (document.Price.Value / document.CompareAtPrice.Value)) * 100, MidpointRounding.AwayFromZero);
            }
        });
    }

    /// <summary>
    /// The public URL of a product image, resized for a card.
    /// </summary>
    public string GetImageUrl(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        var url = _mediaFileStore.MapPathToPublicUrl(path);

        return path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
            ? url
            : url + "?width=640&height=480&rmode=crop";
    }

    private async Task BuildProductsAsync(ProductListingViewModel model, IReadOnlyList<string> ids, IReadOnlyList<ProductAttributeDescriptor> attributes, ProductListingView view)
    {
        if (ids.Count == 0)
        {
            return;
        }

        var documents = (await _session.Query<ProductSearchDocument, ProductSearchIndex>(index => index.ContentItemId.IsIn(ids)).ListAsync())
            .GroupBy(document => document.ContentItemId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var items = (await _contentManager.GetAsync(ids, VersionOptions.Published))
            .Where(item => item is not null)
            .ToDictionary(item => item.ContentItemId, StringComparer.Ordinal);

        foreach (var id in ids)
        {
            if (!documents.TryGetValue(id, out var document) || !items.TryGetValue(id, out var contentItem))
            {
                continue;
            }

            var price = await BuildPriceShapeAsync(document, contentItem.ContentType);
            var shape = await _shapeFactory.CreateAsync<ProductCardViewModel>("ProductCard", card =>
            {
                card.ContentItem = contentItem;
                card.Product = document;
                card.Title = string.IsNullOrEmpty(contentItem.DisplayText) ? document.Title : contentItem.DisplayText;
                card.ImageUrl = GetImageUrl(document.ImagePath);
                card.Price = price;
                card.IsPurchasable = document.IsPurchasable;
                card.Chips = BuildSpecifications(document, attributes, settings => settings.ShowOnCard).Select(row => row.Value).ToList();
                card.Specifications = BuildSpecifications(document, attributes, settings => settings.ShowInSpecifications);
            });

            var type = SafeAlternate(contentItem.ContentType);

            if (view == ProductListingView.List)
            {
                shape.Metadata.Alternates.Add("ProductCard__List");
                shape.Metadata.Alternates.Add($"ProductCard__List__{type}");
            }
            else
            {
                shape.Metadata.Alternates.Add($"ProductCard__{type}");
            }

            model.Products.Add(shape);
        }
    }

    private static ProductFacetViewModel BuildFacet(ProductListingFacet facet, ProductAttributeDescriptor attribute, ProductListingState state, ProductSearchResult result, string currency, Func<ProductListingState, string> urlFor)
    {
        result.Facets.TryGetValue(facet.Key, out var facetResult);

        var model = new ProductFacetViewModel
        {
            Key = facet.Key,
            Label = string.IsNullOrWhiteSpace(facet.Label) ? facet.AttributeKey : facet.Label,
            Kind = facet.Kind,
            Collapsed = facet.Collapsed,
            MaxVisible = facet.MaxVisible,
            IsPrice = facet.AttributeKey == ProductAttributeKeys.Price,
            Currency = currency,
            ClearUrl = urlFor(state.ClearFacet(facet.Key)),
        };

        if (facet.Kind == ProductFacetKind.Range)
        {
            if (state.Ranges.TryGetValue(facet.Key, out var range))
            {
                model.SelectedMin = range.Min;
                model.SelectedMax = range.Max;
                model.HasSelection = true;
            }

            model.AvailableMin = facetResult?.Min;
            model.AvailableMax = facetResult?.Max;
            model.NumberFormat = attribute?.Settings.NumberFormat;
            model.Suffix = attribute?.Settings.Suffix;

            // A range with nothing to range over (no product shows a price) is noise.
            return model.HasSelection || model.AvailableMin.HasValue ? model : null;
        }

        state.Values.TryGetValue(facet.Key, out var selected);
        selected ??= [];

        var values = (facetResult?.Values ?? [])
            .Where(value => facet.Kind != ProductFacetKind.Toggle || value.Value == "true")
            .ToList();

        // A selected option stays visible even when the other filters leave it no products, so it can be cleared.
        foreach (var value in selected.Where(value => values.All(candidate => candidate.Value != value)))
        {
            values.Add(new ProductFacetValue { Value = value, Label = value, Count = 0, Order = int.MaxValue });
        }

        foreach (var value in values
            .Where(value => value.Count > 0 || selected.Contains(value.Value))
            .OrderBy(value => value.Order)
            .ThenBy(value => value.Label, StringComparer.CurrentCultureIgnoreCase))
        {
            model.Options.Add(new ProductFacetOptionViewModel
            {
                Value = value.Value,
                Label = facet.Kind == ProductFacetKind.Toggle ? model.Label : value.Label,
                Count = value.Count,
                Selected = selected.Contains(value.Value),
                Url = urlFor(state.Toggle(facet.Key, value.Value, facet.Kind == ProductFacetKind.Radio)),
            });
        }

        model.HasSelection = selected.Count > 0;

        return model.Options.Count > 0 ? model : null;
    }

    private static void BuildPager(ProductListingViewModel model, ProductListingState state, Func<ProductListingState, string> urlFor)
    {
        if (model.TotalPages <= 1)
        {
            return;
        }

        if (model.Page > 1)
        {
            model.PreviousPageUrl = urlFor(state.WithPage(model.Page - 1));
        }

        if (model.Page < model.TotalPages)
        {
            model.NextPageUrl = urlFor(state.WithPage(model.Page + 1));
        }

        var pages = new SortedSet<int> { 1, model.TotalPages };

        for (var page = model.Page - 2; page <= model.Page + 2; page++)
        {
            if (page >= 1 && page <= model.TotalPages)
            {
                pages.Add(page);
            }
        }

        var previous = 0;

        foreach (var page in pages)
        {
            if (page - previous > 1)
            {
                model.PageLinks.Add(new ProductPageLinkViewModel());
            }

            model.PageLinks.Add(new ProductPageLinkViewModel
            {
                Page = page,
                Url = urlFor(state.WithPage(page)),
                Current = page == model.Page,
            });

            previous = page;
        }
    }

    private async Task<string> GetListingCurrencyAsync(IReadOnlyList<string> types)
    {
        foreach (var type in types)
        {
            var settings = ProductAttributeCatalog.GetProductSettings(await _contentDefinitionManager.GetTypeDefinitionAsync(type));

            if (!string.IsNullOrEmpty(settings?.DefaultCurrency))
            {
                return settings.DefaultCurrency;
            }
        }

        return null;
    }

    private string FormatValue(ProductAttributeValue value, ProductAttributeDescriptor attribute)
    {
        switch (attribute.Kind)
        {
            case ProductAttributeValueKind.Number when value.Number.HasValue:
                var format = string.IsNullOrWhiteSpace(attribute.Settings.NumberFormat) ? "#,0.##" : attribute.Settings.NumberFormat;
                var number = value.Number.Value.ToString(format, CultureInfo.CurrentCulture);

                return string.IsNullOrWhiteSpace(attribute.Settings.Suffix) ? number : $"{number} {attribute.Settings.Suffix}";

            case ProductAttributeValueKind.Boolean:
                return value.Text == "true" ? S["Yes"] : S["No"];

            case ProductAttributeValueKind.Date when value.Date.HasValue:
                return value.Date.Value.ToString("d", CultureInfo.CurrentCulture);

            default:
                return string.IsNullOrEmpty(value.Label) ? value.Text : value.Label;
        }
    }

    private static string FormatRange(ProductFacetViewModel facet)
    {
        var format = string.IsNullOrWhiteSpace(facet.NumberFormat) ? "#,0.##" : facet.NumberFormat;

        string Format(decimal value) => facet.IsPrice
            ? ProductPriceFormatter.Format(value, facet.Currency)
            : string.IsNullOrWhiteSpace(facet.Suffix)
                ? value.ToString(format, CultureInfo.CurrentCulture)
                : $"{value.ToString(format, CultureInfo.CurrentCulture)} {facet.Suffix}";

        return (facet.SelectedMin, facet.SelectedMax) switch
        {
            ({ } min, { } max) => $"{Format(min)} – {Format(max)}",
            ({ } min, null) => $"{Format(min)}+",
            (null, { } max) => $"≤ {Format(max)}",
            _ => string.Empty,
        };
    }

    private static ProductListingFacet Resolve(ProductListingFacet facet, IReadOnlyList<ProductAttributeDescriptor> attributes)
    {
        if (!string.IsNullOrWhiteSpace(facet.Label))
        {
            return facet;
        }

        var attribute = attributes.FirstOrDefault(candidate => candidate.Key == facet.AttributeKey);

        return new ProductListingFacet
        {
            Key = facet.Key,
            AttributeKey = facet.AttributeKey,
            Label = attribute?.Label ?? facet.AttributeKey,
            Kind = facet.Kind,
            Level = facet.Level,
            DependsOn = facet.DependsOn,
            Collapsed = facet.Collapsed,
            MaxVisible = facet.MaxVisible,
        };
    }

    private static void Copy(ProductFacetViewModel source, ProductFacetViewModel target)
    {
        target.Key = source.Key;
        target.Label = source.Label;
        target.Kind = source.Kind;
        target.Collapsed = source.Collapsed;
        target.MaxVisible = source.MaxVisible;
        target.Options = source.Options;
        target.SelectedMin = source.SelectedMin;
        target.SelectedMax = source.SelectedMax;
        target.AvailableMin = source.AvailableMin;
        target.AvailableMax = source.AvailableMax;
        target.NumberFormat = source.NumberFormat;
        target.Suffix = source.Suffix;
        target.IsPrice = source.IsPrice;
        target.Currency = source.Currency;
        target.HasSelection = source.HasSelection;
        target.ClearUrl = source.ClearUrl;
    }

    /// <summary>
    /// Turns an attribute key into a short URL key: <c>ExteriorColor</c> becomes <c>exterior-color</c>.
    /// </summary>
    public static string ToUrlKey(string attributeKey)
    {
        var builder = new StringBuilder();

        foreach (var character in attributeKey ?? string.Empty)
        {
            if (char.IsUpper(character) && builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        return builder.ToString().Trim('-');
    }

    private static string SafeAlternate(string value)
        => new(value.Where(char.IsLetterOrDigit).ToArray());
}
