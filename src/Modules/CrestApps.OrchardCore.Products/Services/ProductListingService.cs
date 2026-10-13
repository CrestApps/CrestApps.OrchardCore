using System.Globalization;
using System.Text;
using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Indexes;
using CrestApps.OrchardCore.Products.Services.Attributes;
using CrestApps.OrchardCore.Products.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Contents;
using OrchardCore.ContentManagement.Display;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
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
    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ProductSearchDocumentBuilder _documentBuilder;
    private readonly Attributes.TaxonomyTermLookup _termLookup;
    private readonly IContentItemDisplayManager _contentItemDisplayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IEnumerable<IProductPositionProvider> _positionProviders;
    private readonly ProductFacetStyleOptions _facetStyles;

    // Several shapes of one page read the same listing or product, and they render in separate passes (the
    // breadcrumb sits in the page header, before the listing). Each is built once per request.
    private readonly Dictionary<string, ProductListingViewModel> _listings = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ProductDetailsViewModel> _products = new(StringComparer.Ordinal);
    private IReadOnlyList<ContentItem> _listingItems;

    // How many automatic filters start open.
    private const int AutomaticOpenFacets = 5;

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
        LinkGenerator linkGenerator,
        IHttpContextAccessor httpContextAccessor,
        ProductSearchDocumentBuilder documentBuilder,
        Attributes.TaxonomyTermLookup termLookup,
        IContentItemDisplayManager contentItemDisplayManager,
        IUpdateModelAccessor updateModelAccessor,
        IEnumerable<IProductPositionProvider> positionProviders,
        IOptions<ProductFacetStyleOptions> facetStyles,
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
        _linkGenerator = linkGenerator;
        _httpContextAccessor = httpContextAccessor;
        _documentBuilder = documentBuilder;
        _termLookup = termLookup;
        _contentItemDisplayManager = contentItemDisplayManager;
        _updateModelAccessor = updateModelAccessor;
        _positionProviders = positionProviders;
        _facetStyles = facetStyles.Value;
        S = stringLocalizer;
    }

    /// <summary>
    /// The filters a listing offers: none when its filters are hidden, its configured ones, or one per filterable
    /// attribute when it is automatic.
    /// </summary>
    public async Task<IReadOnlyList<ProductListingFacet>> GetFacetsAsync(ProductListingPart part, IReadOnlyList<ProductAttributeDescriptor> attributes)
    {
        if (!part.ShowFilters)
        {
            return [];
        }

        if (!part.AutomaticFacets)
        {
            return (part.Facets ?? [])
                .Where(facet => !string.IsNullOrEmpty(facet.Key) && !string.IsNullOrEmpty(facet.AttributeKey))
                .Select(facet => Resolve(facet, attributes))
                .ToArray();
        }

        // Automatic keys come from field names, and a field named Page or Sort must not shadow the listing's own
        // parameters.
        string SafeKey(string key) => ProductListingState.IsReservedKey(key) ? key + "-filter" : key;

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
                Key = SafeKey(ToUrlKey(attribute.Key)),
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

        // A long column of open filters pushes the last ones out of sight; the first few stay open.
        for (var i = AutomaticOpenFacets; i < facets.Count; i++)
        {
            facets[i].Collapsed = true;
        }

        return facets;
    }

    /// <summary>
    /// The sorts a listing offers: its configured ones, or the standard ones plus every sortable attribute.
    /// </summary>
    /// <param name="part">The listing.</param>
    /// <param name="attributes">The attributes of the listed product types.</param>
    /// <param name="offerRecommended">Whether some listed type carries a position, so the "Recommended" sort comes first.</param>
    public IReadOnlyList<ProductListingSort> GetSorts(ProductListingPart part, IReadOnlyList<ProductAttributeDescriptor> attributes, bool offerRecommended = false)
    {
        if (!part.AutomaticSorts && part.Sorts is { Count: > 0 })
        {
            return part.Sorts.Where(sort => !string.IsNullOrEmpty(sort.Key)).ToArray();
        }

        var sorts = new List<ProductListingSort>();

        if (offerRecommended)
        {
            sorts.Add(new ProductListingSort { Key = RecommendedSortKey, Label = S["Recommended"], AttributeKey = ProductAttributeKeys.Position });
        }

        sorts.AddRange(new List<ProductListingSort>
        {
            new() { Key = "newest", Label = S["Newest"], AttributeKey = ProductAttributeKeys.Published, Descending = true },
            new() { Key = "price-asc", Label = S["Price: low to high"], AttributeKey = ProductAttributeKeys.Price },
            new() { Key = "price-desc", Label = S["Price: high to low"], AttributeKey = ProductAttributeKeys.Price, Descending = true },
            new() { Key = "name", Label = S["Name: A to Z"], AttributeKey = ProductAttributeKeys.Title },
        });

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
    /// The style a filter is drawn in, or <see langword="null"/> for the default list: a style that is no longer
    /// registered, or that cannot draw the filter's kind, falls back to the list.
    /// </summary>
    private ProductFacetStyle GetStyle(ProductListingFacet facet)
        => _facetStyles.Find(facet.Style) is { } style && style.Supports(facet.Kind) && style.Name != ProductFacetStyles.List ? style : null;

    /// <summary>
    /// Gives each option its swatch color, from the field's swatch colors, the color field on its term, or its
    /// own name. Any style can use it, not only swatches.
    /// </summary>
    private async Task SetOptionColorsAsync(ProductFacetViewModel facet, ProductAttributeDescriptor attribute)
    {
        if (facet.Kind is ProductFacetKind.Range or ProductFacetKind.Toggle)
        {
            return;
        }

        Dictionary<string, TaxonomyTerm> termsBySlug = null;

        if (!string.IsNullOrWhiteSpace(attribute?.Settings.TermColorField) &&
            await _termLookup.GetTermsAsync(attribute.TaxonomyContentItemId) is { } terms)
        {
            termsBySlug = new Dictionary<string, TaxonomyTerm>(StringComparer.Ordinal);

            foreach (var term in terms.Values)
            {
                termsBySlug.TryAdd(term.Slug ?? term.ContentItemId, term);
                termsBySlug.TryAdd(term.ContentItemId, term);
            }
        }

        foreach (var option in facet.Options)
        {
            var termColor = termsBySlug is not null && option.Value is not null && termsBySlug.TryGetValue(option.Value, out var term)
                ? ProductSwatchColors.ReadTermField(term.Content, attribute.Settings.TermColorField)
                : null;

            option.Color = ProductSwatchColors.Resolve(option.Value, option.Label, attribute?.Settings.SwatchColors, termColor);
        }
    }

    /// <summary>
    /// The sort applied when the visitor picks none: the listing's default sort, or its first sort. A listing that
    /// does not let visitors sort can default to a sort it does not offer, so <paramref name="fallback"/> is searched
    /// too.
    /// </summary>
    public static ProductListingSort GetDefaultSort(ProductListingPart part, IReadOnlyList<ProductListingSort> offered, IReadOnlyList<ProductListingSort> fallback)
        => offered.FirstOrDefault(sort => sort.Key == part.DefaultSort)
            ?? fallback.FirstOrDefault(sort => sort.Key == part.DefaultSort)
            ?? (offered.Count > 0 ? offered[0] : null);

    /// <summary>
    /// The key of the sort by the order the merchant arranged products in.
    /// </summary>
    public const string RecommendedSortKey = "recommended";

    /// <summary>
    /// The product types, among the given ones, whose products carry a position (every product type when none
    /// is given).
    /// </summary>
    public async Task<IReadOnlyList<string>> GetPositionedTypesAsync(IEnumerable<string> contentTypes)
    {
        var requested = contentTypes?.ToArray() ?? [];

        return (await _attributeCatalog.GetProductTypesAsync())
            .Where(type => requested.Length == 0 || requested.Contains(type.Name, StringComparer.Ordinal))
            .Where(type => _positionProviders.Any(provider => provider.HasPositions(type)))
            .Select(type => type.Name)
            .ToArray();
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
    /// Builds a listing page from the request, once per request, with a shape for each of its sections.
    /// </summary>
    public async Task<ProductListingViewModel> GetListingAsync(ProductListingPart part, HttpContext httpContext)
    {
        var key = part.ContentItem.ContentItemVersionId ?? part.ContentItem.ContentItemId ?? string.Empty;

        if (_listings.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var model = new ProductListingViewModel
        {
            ContentItem = part.ContentItem,
            Part = part,
        };

        await BuildAsync(model, part, httpContext);
        await BuildSectionShapesAsync(model);

        _listings[key] = model;

        return model;
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
        var offerRecommended = (await GetPositionedTypesAsync(types)).Count > 0;
        var sorts = GetSorts(part, attributes, offerRecommended);
        var pageSizes = (part.PageSizes is { Length: > 0 } ? part.PageSizes : [12, 24, 48])
            .Where(size => size is > 0 and <= 200)
            .Distinct()
            .Order()
            .ToArray();

        // Without sorting, a sort in the address is ignored, and the default sort may be one the page does not offer.
        var state = ProductListingState.Parse(request.Query, facets, part.ShowSorting ? sorts.Select(sort => sort.Key) : [], pageSizes);

        await MapTermIdsToSlugsAsync(state, facets, attributes);

        var defaultSort = GetDefaultSort(part, sorts, part.ShowSorting ? [] : GetSorts(new ProductListingPart(), attributes, offerRecommended));
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

        model.FormId = "product-listing-" + (part.ContentItem?.ContentItemId ?? "page");
        model.FormAction = baseUrl;
        model.ImageAspectRatio = GetAspectRatio(part.ImageAspectRatio);
        model.EnableKeywordSearch = part.EnableKeywordSearch;
        model.Keyword = state.Keyword;
        model.Sort = sortOption?.Key;
        model.DefaultSort = defaultSort?.Key;
        model.ShowSorting = part.ShowSorting;
        model.Sorts = part.ShowSorting
            ? sorts.Select(sort => new ProductSortOptionViewModel { Key = sort.Key, Label = sort.Label, Selected = sort == sortOption }).ToList()
            : [];
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
        var facetModels = new Dictionary<string, ProductFacetViewModel>(StringComparer.Ordinal);

        foreach (var facet in facets)
        {
            var attribute = attributes.FirstOrDefault(candidate => candidate.Key == facet.AttributeKey);
            var facetModel = BuildFacet(facet, attribute, state, result, currency, UrlFor);

            if (facetModel is null)
            {
                continue;
            }

            facetModel.Style = GetStyle(facet)?.Name;
            await SetOptionColorsAsync(facetModel, attribute);

            facetModels[facet.Key] = facetModel;

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

            if (!string.IsNullOrEmpty(facetModel.Style))
            {
                shape.Metadata.Alternates.Add($"ProductFacet__{SafeAlternate(facetModel.Style)}");
            }

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

        model.ShowFilters = model.Facets.Count > 0 || model.EnableKeywordSearch;

        await BuildProductsAsync(model, result.ContentItemIds, attributes, view, part.ImageAspectRatio);
        BuildPager(model, state, UrlFor);

        model.Breadcrumb = BuildListingBreadcrumb(part.ContentItem, facets, facetModels, state, request.PathBase, baseUrl, UrlFor);
    }

    /// <summary>
    /// The trail of a listing page: the home page, then the categories picked in its tree filters. With
    /// <em>Footwear › Boots</em> picked the trail is Home › Shop › Footwear › Boots.
    /// </summary>
    private ProductBreadcrumbViewModel BuildListingBreadcrumb(
        ContentItem listing,
        IReadOnlyList<ProductListingFacet> facets,
        Dictionary<string, ProductFacetViewModel> facetModels,
        ProductListingState state,
        PathString pathBase,
        string baseUrl,
        Func<ProductListingState, string> urlFor)
    {
        var breadcrumb = new ProductBreadcrumbViewModel
        {
            Name = "ProductListing",
            Title = listing?.DisplayText ?? string.Empty,
        };

        breadcrumb.Items.Add(new ProductBreadcrumbItemViewModel { Id = "Home", Label = S["Home"], Url = pathBase + "/" });

        var chain = new List<ProductBreadcrumbItemViewModel>();
        var trail = new ProductListingState();

        foreach (var facet in GetTreeFacets(facets))
        {
            if (!state.Values.TryGetValue(facet.Key, out var values) ||
                values.Count != 1 ||
                !facetModels.TryGetValue(facet.Key, out var facetModel))
            {
                break;
            }

            var option = facetModel.Options.FirstOrDefault(candidate => candidate.Value == values[0]);

            if (option is null)
            {
                break;
            }

            trail = trail.Toggle(facet.Key, values[0], singleValue: true);
            chain.Add(new ProductBreadcrumbItemViewModel { Id = facet.Key, Label = option.Label, Url = urlFor(trail) });
        }

        if (chain.Count == 0)
        {
            return breadcrumb;
        }

        breadcrumb.Items.Add(new ProductBreadcrumbItemViewModel { Id = "Listing", Label = breadcrumb.Title, Url = baseUrl });

        foreach (var item in chain.Take(chain.Count - 1))
        {
            breadcrumb.Items.Add(item);
        }

        breadcrumb.Title = chain[^1].Label;

        return breadcrumb;
    }

    /// <summary>
    /// The filters that offer the levels of one taxonomy, top level first, such as Make then Model.
    /// </summary>
    private static IEnumerable<ProductListingFacet> GetTreeFacets(IReadOnlyList<ProductListingFacet> facets)
    {
        var first = facets.FirstOrDefault(facet => facet.Level.HasValue && facet.Kind != ProductFacetKind.Range);

        if (first is null)
        {
            return [];
        }

        return facets
            .Where(facet => facet.AttributeKey == first.AttributeKey && facet.Level.HasValue && facet.Kind != ProductFacetKind.Range)
            .OrderBy(facet => facet.Level);
    }

    /// <summary>
    /// Terms are filtered by slug. A link saved before slugs, or built by hand from a term's identifier, still
    /// works: an identifier is swapped for its term's slug.
    /// </summary>
    private async Task MapTermIdsToSlugsAsync(ProductListingState state, IReadOnlyList<ProductListingFacet> facets, IReadOnlyList<ProductAttributeDescriptor> attributes)
    {
        foreach (var facet in facets)
        {
            if (!state.Values.TryGetValue(facet.Key, out var values))
            {
                continue;
            }

            var taxonomyId = attributes.FirstOrDefault(attribute => attribute.Key == facet.AttributeKey)?.TaxonomyContentItemId;

            if (string.IsNullOrEmpty(taxonomyId) || await _termLookup.GetTermsAsync(taxonomyId) is not { } terms)
            {
                continue;
            }

            for (var i = 0; i < values.Count; i++)
            {
                if (terms.TryGetValue(values[i], out var term) && !string.IsNullOrEmpty(term.Slug))
                {
                    values[i] = term.Slug;
                }
            }
        }
    }

    private async Task BuildSectionShapesAsync(ProductListingViewModel model)
    {
        var type = SafeAlternate(model.ContentItem?.ContentType ?? string.Empty);

        async Task<IShape> CreateAsync(string shapeType, params string[] alternates)
        {
            var shape = await _shapeFactory.CreateAsync<ProductListingShapeViewModel>(shapeType, section => section.Listing = model);

            foreach (var alternate in alternates)
            {
                shape.Metadata.Alternates.Add(alternate);
            }

            if (!string.IsNullOrEmpty(type))
            {
                shape.Metadata.Alternates.Add($"{shapeType}__{type}");
            }

            return shape;
        }

        model.SearchShape = model.EnableKeywordSearch ? await CreateAsync("ProductListingSearch") : null;
        model.FiltersShape = await CreateAsync("ProductListingFilters");
        model.ToolbarShape = await CreateAsync("ProductListingToolbar");
        model.ActiveFiltersShape = await CreateAsync("ProductListingActiveFilters");
        model.ResultsShape = await CreateAsync("ProductListingResults", $"ProductListingResults__{model.View}");
        model.PagerShape = await CreateAsync("ProductListingPager");
    }

    /// <summary>
    /// Builds what a product's own page shows, once per request.
    /// </summary>
    public async Task<ProductDetailsViewModel> GetProductAsync(ContentItem contentItem, ProductPageSettings pageSettings, HttpContext httpContext)
    {
        var key = contentItem.ContentItemVersionId ?? contentItem.ContentItemId ?? string.Empty;

        if (_products.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var model = new ProductDetailsViewModel
        {
            ContentItem = contentItem,
            Title = contentItem.DisplayText,
        };

        _products[key] = model;

        var document = await _documentBuilder.BuildAsync(contentItem);

        if (document is null)
        {
            return model;
        }

        var attributes = await _attributeCatalog.GetAttributesAsync([contentItem.ContentType]);

        model.Price = await BuildPriceShapeAsync(document, contentItem.ContentType);
        model.IsPurchasable = document.IsPurchasable;
        model.Sku = document.Sku;
        model.Highlights = BuildSpecifications(document, attributes, attribute => attribute.ShowOnCard).Select(row => row.Value).ToList();
        model.Specifications = BuildSpecifications(document, attributes, attribute => attribute.ShowInSpecifications);
        model.InquiryLabel = pageSettings?.InquiryLabel;
        model.InquiryUrl = string.IsNullOrWhiteSpace(pageSettings?.InquiryUrl)
            ? null
            : pageSettings.InquiryUrl
                .Replace("{id}", Uri.EscapeDataString(contentItem.ContentItemId ?? string.Empty), StringComparison.OrdinalIgnoreCase)
                .Replace("{title}", Uri.EscapeDataString(contentItem.DisplayText ?? string.Empty), StringComparison.OrdinalIgnoreCase);

        var gallery = await GetGalleryFieldAsync(contentItem);

        if (gallery is not null)
        {
            for (var i = 0; i < gallery.Value.Paths.Count; i++)
            {
                var path = gallery.Value.Paths[i];

                model.Images.Add(new ProductImageViewModel
                {
                    Url = GetImageUrl(path, 1200, 0),
                    ThumbnailUrl = GetImageUrl(path, 160, 160),
                    Alt = i < gallery.Value.Texts.Count && !string.IsNullOrWhiteSpace(gallery.Value.Texts[i]) ? gallery.Value.Texts[i] : contentItem.DisplayText,
                });
            }
        }

        model.Breadcrumb = await BuildProductBreadcrumbAsync(contentItem, document, httpContext);

        model.GalleryShape = await _shapeFactory.CreateAsync<ProductDetailsShapeViewModel>("ProductGallery", section => section.Product = model);
        model.GalleryShape.Metadata.Alternates.Add($"ProductGallery__{SafeAlternate(contentItem.ContentType)}");
        model.ProductActions = await BuildProductActionsAsync(contentItem);
        model.ActionsShape = await _shapeFactory.CreateAsync<ProductDetailsShapeViewModel>("ProductActions", section => section.Product = model);
        model.ActionsShape.Metadata.Alternates.Add($"ProductActions__{SafeAlternate(contentItem.ContentType)}");

        return model;
    }

    /// <summary>
    /// The media field whose images a product's page and cards show: the first media field of the type that
    /// has images.
    /// </summary>
    public async Task<(string PartName, string FieldName, IReadOnlyList<string> Paths, IReadOnlyList<string> Texts)?> GetGalleryFieldAsync(ContentItem contentItem)
    {
        var definition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentItem.ContentType);

        if (definition is null)
        {
            return null;
        }

        // Content is a dynamic wrapper over the item's JSON; it converts to the JsonObject only through a cast.
        var content = (System.Text.Json.Nodes.JsonObject)contentItem.Content;

        foreach (var part in definition.Parts)
        {
            foreach (var field in part.PartDefinition.Fields.Where(field => field.FieldDefinition.Name == "MediaField"))
            {
                if (content[part.Name]?[field.Name] is not System.Text.Json.Nodes.JsonObject media)
                {
                    continue;
                }

                var paths = Attributes.FieldValueReader.GetStrings(media, "Paths").Where(path => !string.IsNullOrEmpty(path)).ToArray();

                if (paths.Length > 0)
                {
                    return (part.Name, field.Name, paths, Attributes.FieldValueReader.GetStrings(media, "MediaTexts").ToArray());
                }
            }
        }

        return null;
    }

    /// <summary>
    /// The trail of a product's page: the home page, the listing that shows the product's type, and the
    /// product's place in that listing's category tree.
    /// </summary>
    private async Task<ProductBreadcrumbViewModel> BuildProductBreadcrumbAsync(ContentItem contentItem, ProductSearchDocument document, HttpContext httpContext)
    {
        var pathBase = httpContext?.Request.PathBase ?? PathString.Empty;
        var breadcrumb = new ProductBreadcrumbViewModel
        {
            Name = "Product",
            Title = contentItem.DisplayText ?? string.Empty,
        };

        breadcrumb.Items.Add(new ProductBreadcrumbItemViewModel { Id = "Home", Label = S["Home"], Url = pathBase + "/" });

        var listing = await FindListingAsync(contentItem.ContentType);

        if (listing is null || httpContext is null)
        {
            return breadcrumb;
        }

        var metadata = await _contentManager.PopulateAspectAsync<ContentItemMetadata>(listing);
        var listingUrl = metadata.DisplayRouteValues is null
            ? null
            : _linkGenerator.GetPathByRouteValues(httpContext, routeName: null, metadata.DisplayRouteValues);

        if (string.IsNullOrEmpty(listingUrl))
        {
            return breadcrumb;
        }

        breadcrumb.Items.Add(new ProductBreadcrumbItemViewModel { Id = "Listing", Label = listing.DisplayText, Url = listingUrl });

        if (!listing.TryGet<ProductListingPart>(out var part))
        {
            return breadcrumb;
        }

        var types = await GetContentTypesAsync(part);
        var attributes = await _attributeCatalog.GetAttributesAsync(types);
        var trail = new ProductListingState();

        foreach (var facet in GetTreeFacets(await GetFacetsAsync(part, attributes)))
        {
            var value = document.Attributes.FirstOrDefault(candidate => candidate.Key == facet.AttributeKey && candidate.Level == facet.Level);

            if (value is null || string.IsNullOrEmpty(value.Text))
            {
                break;
            }

            trail = trail.Toggle(facet.Key, value.Text, singleValue: true);
            breadcrumb.Items.Add(new ProductBreadcrumbItemViewModel
            {
                Id = facet.Key,
                Label = string.IsNullOrEmpty(value.Label) ? value.Text : value.Label,
                Url = listingUrl + trail.ToQueryString(),
            });
        }

        return breadcrumb;
    }

    /// <summary>
    /// The listing page a product belongs to: the first one that names the product's type, or else the first
    /// one that lists every type.
    /// </summary>
    private async Task<ContentItem> FindListingAsync(string contentType)
    {
        if (_listingItems is null)
        {
            var listingTypes = (await _contentDefinitionManager.ListTypeDefinitionsAsync())
                .Where(type => type.Parts.Any(part => part.PartDefinition.Name == nameof(ProductListingPart)))
                .Select(type => type.Name)
                .ToArray();

            _listingItems = listingTypes.Length == 0
                ? []
                : await _session.Query<ContentItem, ContentItemIndex>(index => index.Published && index.ContentType.IsIn(listingTypes))
                    .OrderBy(index => index.CreatedUtc)
                    .Take(50)
                    .ListAsync();
        }

        ContentItem fallback = null;

        foreach (var item in _listingItems)
        {
            if (!item.TryGet<ProductListingPart>(out var part))
            {
                continue;
            }

            if (part.ContentTypes is { Length: > 0 } listed)
            {
                if (listed.Contains(contentType, StringComparer.Ordinal))
                {
                    return item;
                }
            }
            else
            {
                fallback ??= item;
            }
        }

        return fallback;
    }

    /// <summary>
    /// How often a price is charged: "/ month" for every month, "every 3 months" for a longer period.
    /// </summary>
    private string FormatPeriod(BillingInterval? interval, int? duration)
    {
        if (interval is null)
        {
            return null;
        }

        var count = Math.Max(1, duration ?? 1);

        return (interval.Value, count) switch
        {
            (BillingInterval.Day, 1) => S["/ day"],
            (BillingInterval.Week, 1) => S["/ week"],
            (BillingInterval.Month, 1) => S["/ month"],
            (BillingInterval.Year, 1) => S["/ year"],
            (BillingInterval.Day, _) => S["every {0} days", count],
            (BillingInterval.Week, _) => S["every {0} weeks", count],
            (BillingInterval.Month, _) => S["every {0} months", count],
            _ => S["every {0} years", count],
        };
    }

    /// <summary>
    /// What modules offer to do with a product: its <c>ProductAction</c> display. A module places a shape in its
    /// Actions zone the Orchard way, for example <c>.Location("ProductAction", "Actions")</c>, and the shape shows on
    /// the product's card and page. Subscriptions places its subscribe form there.
    /// </summary>
    public async Task<IShape> BuildProductActionsAsync(ContentItem contentItem)
    {
        var shape = await _contentItemDisplayManager.BuildDisplayAsync(contentItem, _updateModelAccessor.ModelUpdater, ProductActionDisplayType);

        // A type with a stereotype renders as "{Stereotype}_ProductAction" (Subscription_ProductAction for a plan),
        // which no theme defines. Every product's actions render through Content_ProductAction instead; a
        // template for the stereotype still wins through the alternates the display manager added.
        if (shape is not null && shape.Metadata.Type != ContentProductActionShapeType)
        {
            shape.Metadata.Type = ContentProductActionShapeType;
        }

        return shape;
    }

    private const string ContentProductActionShapeType = "Content_ProductAction";

    /// <summary>
    /// The display type a module places its product actions in.
    /// </summary>
    public const string ProductActionDisplayType = "ProductAction";

    private static string GetAspectRatio(ProductImageAspectRatio ratio)
        => ratio switch
        {
            ProductImageAspectRatio.Square => "1 / 1",
            ProductImageAspectRatio.Portrait => "3 / 4",
            ProductImageAspectRatio.Wide => "16 / 9",
            _ => "4 / 3",
        };

    private static (int Width, int Height) GetCardImageSize(ProductImageAspectRatio ratio)
        => ratio switch
        {
            ProductImageAspectRatio.Square => (640, 640),
            ProductImageAspectRatio.Portrait => (480, 640),
            ProductImageAspectRatio.Wide => (640, 360),
            _ => (640, 480),
        };

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
            model.Period = FormatPeriod(document.BillingInterval, document.BillingDuration);

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
        => GetImageUrl(path, 640, 480);

    /// <summary>
    /// The public URL of a product image, resized and cropped to a size. A height of zero keeps the image's own
    /// proportions.
    /// </summary>
    public string GetImageUrl(string path, int width, int height)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        var url = _mediaFileStore.MapPathToPublicUrl(path);

        if (path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        return height > 0
            ? $"{url}?width={width}&height={height}&rmode=crop"
            : $"{url}?width={width}&rmode=max";
    }

    private async Task BuildProductsAsync(ProductListingViewModel model, IReadOnlyList<string> ids, IReadOnlyList<ProductAttributeDescriptor> attributes, ProductListingView view, ProductImageAspectRatio ratio)
    {
        var (imageWidth, imageHeight) = GetCardImageSize(ratio);
        var salesModes = new Dictionary<string, ProductSalesMode>(StringComparer.Ordinal);

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

            if (!salesModes.TryGetValue(contentItem.ContentType, out var salesMode))
            {
                salesMode = ProductAttributeCatalog.GetProductSettings(await _contentDefinitionManager.GetTypeDefinitionAsync(contentItem.ContentType))?.SalesMode ?? ProductSalesMode.Purchasable;
                salesModes[contentItem.ContentType] = salesMode;
            }

            var metadata = await _contentManager.PopulateAspectAsync<ContentItemMetadata>(contentItem);
            var url = metadata.DisplayRouteValues is null || _httpContextAccessor.HttpContext is null
                ? null
                : _linkGenerator.GetPathByRouteValues(_httpContextAccessor.HttpContext, routeName: null, metadata.DisplayRouteValues);

            var price = await BuildPriceShapeAsync(document, contentItem.ContentType);
            var actions = await BuildProductActionsAsync(contentItem);
            var shape = await _shapeFactory.CreateAsync<ProductCardViewModel>("ProductCard", card =>
            {
                card.ContentItem = contentItem;
                card.Product = document;
                card.Title = string.IsNullOrEmpty(contentItem.DisplayText) ? document.Title : contentItem.DisplayText;
                card.Url = url;
                card.ImageUrl = GetImageUrl(document.ImagePath, imageWidth, imageHeight);
                card.ImageAspectRatio = model.ImageAspectRatio;
                card.Price = price;
                card.DiscountPercent = document.PriceDisplay == ProductPriceDisplay.Show && document.Price > 0 && document.CompareAtPrice > document.Price
                    ? (int)Math.Round((1 - (document.Price.Value / document.CompareAtPrice.Value)) * 100, MidpointRounding.AwayFromZero)
                    : null;
                card.IsPurchasable = document.IsPurchasable;
                card.ShowAvailability = salesMode == ProductSalesMode.PerItem;
                card.Actions = actions;
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
            CurrencySymbol = facet.AttributeKey == ProductAttributeKeys.Price ? ProductPriceFormatter.GetSymbol(currency) : null,
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

            // The merchant's own ranges, or round ones worked out from the products' values.
            IEnumerable<(decimal? Min, decimal? Max)> buckets = !facet.ShowRanges
                ? []
                : facet.Ranges is { Count: > 0 }
                    ? facet.Ranges.Select(range => (range.Min, range.Max))
                    : facetResult?.Min is { } low && facetResult.Max is { } high ? GetRangeBuckets(low, high) : [];

            foreach (var (min, max) in buckets)
            {
                var target = state.ClearFacet(facet.Key);
                target.Ranges[facet.Key] = (min, max);

                model.Ranges.Add(new ProductFacetRangeViewModel
                {
                    Min = min,
                    Max = max,
                    Label = FormatRange(model, min, max),
                    Url = urlFor(target),
                    Selected = model.HasSelection && model.SelectedMin == min && model.SelectedMax == max,
                });
            }

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
        model.SelectedCount = selected.Count;

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
        => FormatRange(facet, facet.SelectedMin, facet.SelectedMax);

    private static string FormatRange(ProductFacetViewModel facet, decimal? low, decimal? high)
    {
        var format = string.IsNullOrWhiteSpace(facet.NumberFormat) ? "#,0.##" : facet.NumberFormat;

        string Format(decimal value) => facet.IsPrice
            ? ProductPriceFormatter.Format(value, facet.Currency)
            : string.IsNullOrWhiteSpace(facet.Suffix)
                ? value.ToString(format, CultureInfo.CurrentCulture)
                : $"{value.ToString(format, CultureInfo.CurrentCulture)} {facet.Suffix}";

        return (low, high) switch
        {
            ({ } min, { } max) => $"{Format(min)} – {Format(max)}",
            ({ } min, null) => $"{Format(min)}+",
            (null, { } max) => $"≤ {Format(max)}",
            _ => string.Empty,
        };
    }

    private static ProductListingFacet Resolve(ProductListingFacet facet, IReadOnlyList<ProductAttributeDescriptor> attributes)
    {
        // The editor refuses a reserved key, but a recipe or a deployment does not go through the editor.
        var key = ProductListingState.IsReservedKey(facet.Key) ? facet.Key + "-filter" : facet.Key;

        if (!string.IsNullOrWhiteSpace(facet.Label) && key == facet.Key)
        {
            return facet;
        }

        var attribute = attributes.FirstOrDefault(candidate => candidate.Key == facet.AttributeKey);

        return new ProductListingFacet
        {
            Key = key,
            AttributeKey = facet.AttributeKey,
            Label = string.IsNullOrWhiteSpace(facet.Label) ? attribute?.Label ?? facet.AttributeKey : facet.Label,
            Kind = facet.Kind,
            Level = facet.Level,
            DependsOn = facet.DependsOn,
            Collapsed = facet.Collapsed,
            MaxVisible = facet.MaxVisible,
            Style = facet.Style,
            ShowRanges = facet.ShowRanges,
            Ranges = facet.Ranges ?? [],
        };
    }

    /// <summary>
    /// Splits a range into about four ready-made ranges with round edges: 10.99 to 249 becomes up to 50, 50 to
    /// 100, 100 to 150, 150 to 200 and 200 and up.
    /// </summary>
    /// <returns>Each range's lower and upper edge, either of which may be open.</returns>
    public static IReadOnlyList<(decimal? Min, decimal? Max)> GetRangeBuckets(decimal min, decimal max, int count = 4)
    {
        if (max <= min || count < 2)
        {
            return [];
        }

        var step = GetNiceStep((max - min) / count);

        if (step <= 0)
        {
            return [];
        }

        // A step rounded down can leave too many ranges to read at a glance; take the next round step instead.
        while ((max - min) / step > count + 1.5m)
        {
            step = GetNiceStep(step * 1.01m, roundUp: true);
        }

        var edges = new List<decimal>();

        for (var edge = (Math.Floor(min / step) + 1) * step; edge < max && edges.Count < 10; edge += step)
        {
            edges.Add(edge);
        }

        if (edges.Count == 0)
        {
            return [];
        }

        var buckets = new List<(decimal? Min, decimal? Max)> { (null, edges[0]) };

        for (var i = 1; i < edges.Count; i++)
        {
            buckets.Add((edges[i - 1], edges[i]));
        }

        buckets.Add((edges[^1], null));

        return buckets;
    }

    private static decimal GetNiceStep(decimal rough, bool roundUp = false)
    {
        if (rough <= 0)
        {
            return 0;
        }

        // The round step nearest the rough one (57.75 becomes 50, 2.25 becomes 2), or the next one up.
        var candidates = new[] { 1m, 2m, 2.5m, 5m, 10m };
        var magnitude = (decimal)Math.Pow(10, Math.Floor(Math.Log10((double)rough)));
        var fraction = rough / magnitude;
        var nice = roundUp
            ? candidates.First(candidate => candidate >= fraction)
            : candidates.MinBy(candidate => Math.Abs(candidate - fraction));

        return nice * magnitude;
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
        target.CurrencySymbol = source.CurrencySymbol;
        target.HasSelection = source.HasSelection;
        target.SelectedCount = source.SelectedCount;
        target.Style = source.Style;
        target.Ranges = source.Ranges;
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
