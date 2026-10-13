using CrestApps.OrchardCore.Products.Core.Listing;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Products.ViewModels;

public class ProductListingPartViewModel
{
    public string[] SelectedContentTypes { get; set; } = [];

    public bool AutomaticFacets { get; set; }

    public List<ProductListingFacetRow> Facets { get; set; } = [];

    public bool AutomaticSorts { get; set; }

    public List<ProductListingSortRow> Sorts { get; set; } = [];

    public string DefaultSort { get; set; }

    public ProductListingView DefaultView { get; set; }

    public bool AllowViewSwitching { get; set; }

    public ProductImageAspectRatio ImageAspectRatio { get; set; }

    public string PageSizes { get; set; }

    public int DefaultPageSize { get; set; }

    public bool EnableKeywordSearch { get; set; }

    [BindNever]
    public List<ProductListingTypeOption> ContentTypes { get; set; } = [];

    [BindNever]
    public List<SelectListItem> SortOptions { get; set; } = [];

    [BindNever]
    public List<SelectListItem> Views { get; set; } = [];

    public List<SelectListItem> ImageAspectRatios { get; set; } = [];

    public List<SelectListItem> FacetDisplays { get; set; } = [];

    [BindNever]
    public List<SelectListItem> FacetKinds { get; set; } = [];
}

public class ProductListingTypeOption
{
    public string Name { get; set; }

    public string DisplayName { get; set; }

    public bool Selected { get; set; }
}

public class ProductListingFacetRow
{
    public bool Enabled { get; set; }

    public string AttributeKey { get; set; }

    public string AttributeLabel { get; set; }

    public bool IsTerm { get; set; }

    public string Key { get; set; }

    public string Label { get; set; }

    public ProductFacetKind Kind { get; set; }

    public int? Level { get; set; }

    public string DependsOn { get; set; }

    public bool Collapsed { get; set; }

    public int MaxVisible { get; set; }

    public ProductFacetDisplay Display { get; set; }

    public bool ShowRanges { get; set; }

    public int Order { get; set; }

    /// <summary>
    /// The product types whose field the filter works on, or none for a filter every product has, such as the
    /// price. Used to group the editor's rows; never posted.
    /// </summary>
    [BindNever]
    public IList<string> ContentTypes { get; set; } = [];
}

public class ProductListingSortRow
{
    public bool Enabled { get; set; }

    public string Key { get; set; }

    public string Label { get; set; }

    public string AttributeKey { get; set; }

    public bool Descending { get; set; }

    public int Order { get; set; }

    /// <summary>
    /// The product types whose field the sort works on, or none for a sort every product has, such as the price.
    /// Used to group the editor's rows; never posted.
    /// </summary>
    [BindNever]
    public IList<string> ContentTypes { get; set; } = [];
}
