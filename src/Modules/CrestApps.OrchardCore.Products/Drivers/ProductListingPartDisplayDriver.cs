using System.Globalization;
using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Services;
using CrestApps.OrchardCore.Products.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Products.Drivers;

public sealed class ProductListingPartDisplayDriver : ContentPartDisplayDriver<ProductListingPart>
{
    private readonly ProductListingService _listingService;
    private readonly ProductAttributeCatalog _attributeCatalog;
    private readonly IHttpContextAccessor _httpContextAccessor;

    internal readonly IStringLocalizer S;

    public ProductListingPartDisplayDriver(
        ProductListingService listingService,
        ProductAttributeCatalog attributeCatalog,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<ProductListingPartDisplayDriver> stringLocalizer)
    {
        _listingService = listingService;
        _attributeCatalog = attributeCatalog;
        _httpContextAccessor = httpContextAccessor;
        S = stringLocalizer;
    }

    public override IDisplayResult Display(ProductListingPart part, BuildPartDisplayContext context)
    {
        if (context.DisplayType != "Detail")
        {
            return null;
        }

        // The trail sits in the page header, above the title, and the listing in the content. Both read the same
        // listing, which the service builds once per request.
        return Combine(
            Initialize<ProductListingShapeViewModel>(GetDisplayShapeType(context), async model =>
                model.Listing = await _listingService.GetListingAsync(part, _httpContextAccessor.HttpContext))
                .Location("Detail", "Content:5"),
            Initialize<ProductListingShapeViewModel>("ProductListingBreadcrumb", async model =>
                model.Listing = await _listingService.GetListingAsync(part, _httpContextAccessor.HttpContext))
                .Location("Detail", "Header:before"));
    }

    public override IDisplayResult Edit(ProductListingPart part, BuildPartEditorContext context)
    {
        var postedOnlyTheseTypes = PostedOnlyTheseTypes();

        return Initialize<ProductListingPartViewModel>(GetEditorShapeType(context), async model =>
        {
            var productTypes = await _attributeCatalog.GetProductTypesAsync();
            var selectedTypes = part.ContentTypes ?? [];

            // A save refused for picking "only these types" with none ticked shows that choice again, not "every type".
            model.ListEveryType = selectedTypes.Length == 0 && !postedOnlyTheseTypes;
            model.ContentTypes = productTypes
                .Select(type => new ProductListingTypeOption
                {
                    Name = type.Name,
                    DisplayName = type.DisplayName,
                    Selected = selectedTypes.Contains(type.Name, StringComparer.Ordinal),
                })
                .ToList();

            // Every product type's filters and sorts are offered, grouped by type, and the editor shows the groups of
            // the types that are ticked; ticking another type needs no save.
            var attributes = await _attributeCatalog.GetAttributesAsync([]);
            var configured = part.Facets ?? [];
            var candidates = await _listingService.GetFacetsAsync(new ProductListingPart { AutomaticFacets = true }, attributes);

            model.ShowFilters = part.ShowFilters;
            model.AutomaticFacets = part.AutomaticFacets;
            model.Facets = BuildFacetRows(configured, candidates, attributes);
            model.ShowSorting = part.ShowSorting;
            model.AutomaticSorts = part.AutomaticSorts;
            var positioned = await _listingService.GetPositionedTypesAsync([]);
            model.Sorts = BuildSortRows(part.Sorts ?? [], _listingService.GetSorts(new ProductListingPart { AutomaticSorts = true }, attributes, positioned.Count > 0), attributes, positioned);
            model.DefaultSort = part.DefaultSort;
            model.DefaultView = part.DefaultView;
            model.AllowViewSwitching = part.AllowViewSwitching;
            model.ImageAspectRatio = part.ImageAspectRatio;
            model.FacetDisplays =
            [
                new SelectListItem(S["List"], nameof(ProductFacetDisplay.List)),
                new SelectListItem(S["Buttons"], nameof(ProductFacetDisplay.Buttons)),
                new SelectListItem(S["Color swatches"], nameof(ProductFacetDisplay.Swatches)),
            ];
            model.ImageAspectRatios =
            [
                new SelectListItem(S["Landscape (4:3)"], nameof(ProductImageAspectRatio.Landscape)),
                new SelectListItem(S["Square (1:1)"], nameof(ProductImageAspectRatio.Square)),
                new SelectListItem(S["Portrait (3:4)"], nameof(ProductImageAspectRatio.Portrait)),
                new SelectListItem(S["Wide (16:9)"], nameof(ProductImageAspectRatio.Wide)),
            ];
            model.PageSizes = string.Join(", ", part.PageSizes ?? []);
            model.DefaultPageSize = part.DefaultPageSize;
            model.EnableKeywordSearch = part.EnableKeywordSearch;
            model.SortOptions = model.Sorts.Select(sort => new SelectListItem(sort.Label, sort.Key)).ToList();
            model.Views =
            [
                new SelectListItem(S["Grid"], nameof(ProductListingView.Grid)),
                new SelectListItem(S["List"], nameof(ProductListingView.List)),
            ];
            model.FacetKinds =
            [
                new SelectListItem(S["Checkboxes (pick several)"], nameof(ProductFacetKind.Checkbox)),
                new SelectListItem(S["Radio buttons (pick one)"], nameof(ProductFacetKind.Radio)),
                new SelectListItem(S["Range (minimum and maximum)"], nameof(ProductFacetKind.Range)),
                new SelectListItem(S["Toggle (yes only)"], nameof(ProductFacetKind.Toggle)),
            ];
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(ProductListingPart part, UpdatePartEditorContext context)
    {
        var model = new ProductListingPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        part.ContentTypes = model.ListEveryType
            ? []
            : model.SelectedContentTypes?.Where(type => !string.IsNullOrEmpty(type)).Distinct(StringComparer.Ordinal).ToArray() ?? [];

        if (!model.ListEveryType && part.ContentTypes.Length == 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.SelectedContentTypes), S["Pick at least one product type, or list every product type."]);
        }

        // A filter or sort on a field of a type that is not listed has nothing to work on.
        var attributes = await _attributeCatalog.GetAttributesAsync([]);
        bool Applies(string attributeKey)
            => part.ContentTypes.Length == 0 ||
               attributes.FirstOrDefault(attribute => attribute.Key == attributeKey) is not { } attribute ||
               attribute.ContentTypes.Any(type => part.ContentTypes.Contains(type, StringComparer.Ordinal));
        part.ShowFilters = model.ShowFilters;
        part.AutomaticFacets = model.AutomaticFacets;
        part.ShowSorting = model.ShowSorting;
        part.AutomaticSorts = model.AutomaticSorts;
        part.DefaultSort = model.DefaultSort;
        part.DefaultView = model.DefaultView;
        part.AllowViewSwitching = model.AllowViewSwitching;
        part.ImageAspectRatio = model.ImageAspectRatio;
        part.EnableKeywordSearch = model.EnableKeywordSearch;

        var sizes = (model.PageSizes ?? string.Empty)
            .Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries)
            .Select(value => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var size) ? size : 0)
            .Where(size => size is > 0 and <= 200)
            .Distinct()
            .Order()
            .ToArray();

        if (sizes.Length == 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.PageSizes), S["Enter at least one page size between 1 and 200."]);
        }
        else
        {
            part.PageSizes = sizes;
        }

        part.DefaultPageSize = sizes.Contains(model.DefaultPageSize) ? model.DefaultPageSize : sizes.FirstOrDefault(24);

        var facets = (model.Facets ?? [])
            .Where(row => row.Enabled && !string.IsNullOrWhiteSpace(row.AttributeKey) && Applies(row.AttributeKey))
            .OrderBy(row => row.Order)
            .Select(row => new ProductListingFacet
            {
                Key = ProductListingService.ToUrlKey(string.IsNullOrWhiteSpace(row.Key) ? row.AttributeKey : row.Key),
                AttributeKey = row.AttributeKey,
                Label = row.Label?.Trim(),
                Kind = row.Kind,
                Level = row.Level is > 0 ? row.Level : null,
                DependsOn = string.IsNullOrWhiteSpace(row.DependsOn) ? null : ProductListingService.ToUrlKey(row.DependsOn),
                Collapsed = row.Collapsed,
                MaxVisible = Math.Max(0, row.MaxVisible),
                Display = row.Display,
                ShowRanges = row.ShowRanges,
            })
            .ToList();

        var reserved = new[]
        {
            ProductListingState.KeywordKey,
            ProductListingState.SortKey,
            ProductListingState.ViewKey,
            ProductListingState.PageKey,
            ProductListingState.PageSizeKey,
        };

        foreach (var duplicate in facets.GroupBy(facet => facet.Key).Where(group => group.Count() > 1 || reserved.Contains(group.Key)))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Facets), S["The URL key '{0}' is used more than once or is reserved. Give each filter its own key.", duplicate.Key]);
        }

        part.Facets = facets;

        part.Sorts = (model.Sorts ?? [])
            .Where(row => row.Enabled && !string.IsNullOrWhiteSpace(row.AttributeKey) && Applies(row.AttributeKey))
            .OrderBy(row => row.Order)
            .Select(row => new ProductListingSort
            {
                Key = ProductListingService.ToUrlKey(row.Key),
                Label = row.Label?.Trim(),
                AttributeKey = row.AttributeKey,
                Descending = row.Descending,
            })
            .ToList();

        return Edit(part, context);
    }

    private bool PostedOnlyTheseTypes()
    {
        var request = _httpContextAccessor.HttpContext?.Request;

        return request is not null &&
            request.HasFormContentType &&
            string.Equals(request.Form[$"{Prefix}.{nameof(ProductListingPartViewModel.ListEveryType)}"], "false", StringComparison.OrdinalIgnoreCase);
    }

    private static List<ProductListingFacetRow> BuildFacetRows(
        List<ProductListingFacet> configured,
        IReadOnlyList<ProductListingFacet> candidates,
        IReadOnlyList<ProductAttributeDescriptor> attributes)
    {
        var rows = new List<ProductListingFacetRow>();
        var order = 0;

        foreach (var facet in configured)
        {
            rows.Add(ToRow(facet, enabled: true, order++, attributes));
        }

        foreach (var candidate in candidates.Where(candidate => configured.All(facet => facet.AttributeKey != candidate.AttributeKey)))
        {
            rows.Add(ToRow(candidate, enabled: configured.Count == 0, order++, attributes));
        }

        return rows;
    }

    private static ProductListingFacetRow ToRow(ProductListingFacet facet, bool enabled, int order, IReadOnlyList<ProductAttributeDescriptor> attributes)
    {
        var attribute = attributes.FirstOrDefault(candidate => candidate.Key == facet.AttributeKey);

        return new ProductListingFacetRow
        {
            Enabled = enabled,
            AttributeKey = facet.AttributeKey,
            AttributeLabel = attribute?.Label ?? facet.Label ?? facet.AttributeKey,
            IsTerm = attribute?.Kind == ProductAttributeValueKind.Term,
            Key = facet.Key,
            Label = facet.Label,
            Kind = facet.Kind,
            Level = facet.Level,
            DependsOn = facet.DependsOn,
            Collapsed = facet.Collapsed,
            MaxVisible = facet.MaxVisible,
            Display = facet.Display,
            ShowRanges = facet.ShowRanges,
            Order = order,
            ContentTypes = attribute?.ContentTypes.ToList() ?? [],
        };
    }

    private static List<ProductListingSortRow> BuildSortRows(
        List<ProductListingSort> configured,
        IReadOnlyList<ProductListingSort> candidates,
        IReadOnlyList<ProductAttributeDescriptor> attributes,
        IReadOnlyList<string> positionedTypes)
    {
        var rows = new List<ProductListingSortRow>();
        var order = 0;

        // The "Recommended" sort belongs with the types that carry a position.
        List<string> TypesOf(string attributeKey)
            => attributeKey == ProductAttributeKeys.Position
                ? positionedTypes.ToList()
                : attributes.FirstOrDefault(attribute => attribute.Key == attributeKey)?.ContentTypes.ToList() ?? [];

        foreach (var sort in configured)
        {
            rows.Add(new ProductListingSortRow { Enabled = true, Key = sort.Key, Label = sort.Label, AttributeKey = sort.AttributeKey, Descending = sort.Descending, Order = order++, ContentTypes = TypesOf(sort.AttributeKey) });
        }

        foreach (var candidate in candidates.Where(candidate => configured.All(sort => sort.Key != candidate.Key)))
        {
            rows.Add(new ProductListingSortRow { Enabled = configured.Count == 0, Key = candidate.Key, Label = candidate.Label, AttributeKey = candidate.AttributeKey, Descending = candidate.Descending, Order = order++, ContentTypes = TypesOf(candidate.AttributeKey) });
        }

        return rows;
    }
}
