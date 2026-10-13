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
        return Initialize<ProductListingPartViewModel>(GetEditorShapeType(context), async model =>
        {
            var productTypes = await _attributeCatalog.GetProductTypesAsync();
            var selectedTypes = part.ContentTypes ?? [];

            model.ContentTypes = productTypes
                .Select(type => new ProductListingTypeOption
                {
                    Name = type.Name,
                    DisplayName = type.DisplayName,
                    Selected = selectedTypes.Contains(type.Name, StringComparer.Ordinal),
                })
                .ToList();

            var attributes = await _attributeCatalog.GetAttributesAsync(selectedTypes);
            var configured = part.Facets ?? [];
            var candidates = await _listingService.GetFacetsAsync(new ProductListingPart { ContentTypes = selectedTypes, AutomaticFacets = true }, attributes);

            model.AutomaticFacets = part.AutomaticFacets;
            model.Facets = BuildFacetRows(configured, candidates, attributes);
            model.AutomaticSorts = part.AutomaticSorts;
            model.Sorts = BuildSortRows(part.Sorts ?? [], _listingService.GetSorts(new ProductListingPart { AutomaticSorts = true }, attributes));
            model.DefaultSort = part.DefaultSort;
            model.DefaultView = part.DefaultView;
            model.AllowViewSwitching = part.AllowViewSwitching;
            model.ImageAspectRatio = part.ImageAspectRatio;
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

        part.ContentTypes = model.SelectedContentTypes?.Where(type => !string.IsNullOrEmpty(type)).Distinct(StringComparer.Ordinal).ToArray() ?? [];
        part.AutomaticFacets = model.AutomaticFacets;
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
            .Where(row => row.Enabled && !string.IsNullOrWhiteSpace(row.AttributeKey))
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
            .Where(row => row.Enabled && !string.IsNullOrWhiteSpace(row.AttributeKey))
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
            Order = order,
        };
    }

    private static List<ProductListingSortRow> BuildSortRows(List<ProductListingSort> configured, IReadOnlyList<ProductListingSort> candidates)
    {
        var rows = new List<ProductListingSortRow>();
        var order = 0;

        foreach (var sort in configured)
        {
            rows.Add(new ProductListingSortRow { Enabled = true, Key = sort.Key, Label = sort.Label, AttributeKey = sort.AttributeKey, Descending = sort.Descending, Order = order++ });
        }

        foreach (var candidate in candidates.Where(candidate => configured.All(sort => sort.Key != candidate.Key)))
        {
            rows.Add(new ProductListingSortRow { Enabled = configured.Count == 0, Key = candidate.Key, Label = candidate.Label, AttributeKey = candidate.AttributeKey, Descending = candidate.Descending, Order = order++ });
        }

        return rows;
    }
}
