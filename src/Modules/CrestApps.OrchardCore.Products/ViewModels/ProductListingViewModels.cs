using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Core.Models;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Products.ViewModels;

/// <summary>
/// Everything a listing page shows. Each section of the page is its own shape, built from this model, so a theme
/// can override one section without the others, in Razor or in Liquid.
/// </summary>
public class ProductListingViewModel
{
    public ContentItem ContentItem { get; set; }

    public ProductListingPart Part { get; set; }

    /// <summary>
    /// The HTML id of the filter form. Controls outside the form, such as the sort, use it to join the form.
    /// </summary>
    public string FormId { get; set; }

    public string FormAction { get; set; }

    public bool EnableKeywordSearch { get; set; }

    /// <summary>
    /// Whether the page has a filters panel: it offers filters or keyword search. Without one, the results take
    /// the full width.
    /// </summary>
    public bool ShowFilters { get; set; }

    /// <summary>
    /// Whether visitors can change the sort.
    /// </summary>
    public bool ShowSorting { get; set; }

    public string Keyword { get; set; }

    public IList<IShape> Facets { get; set; } = [];

    public IList<IShape> Products { get; set; } = [];

    public IList<ProductActiveFilterViewModel> ActiveFilters { get; set; } = [];

    public string ClearAllUrl { get; set; }

    public IList<ProductSortOptionViewModel> Sorts { get; set; } = [];

    public string Sort { get; set; }

    /// <summary>
    /// The sort used when the address names none, left out of the address when picked.
    /// </summary>
    public string DefaultSort { get; set; }

    public ProductListingView View { get; set; }

    public ProductListingView DefaultView { get; set; }

    public bool AllowViewSwitching { get; set; }

    public string GridUrl { get; set; }

    public string ListUrl { get; set; }

    /// <summary>
    /// The width-to-height ratio of product images, as a CSS <c>aspect-ratio</c> value such as <c>4 / 3</c>.
    /// </summary>
    public string ImageAspectRatio { get; set; }

    public IList<int> PageSizes { get; set; } = [];

    public int PageSize { get; set; }

    public int DefaultPageSize { get; set; }

    public int Page { get; set; }

    public int Total { get; set; }

    public int TotalPages { get; set; }

    public IList<ProductPageLinkViewModel> PageLinks { get; set; } = [];

    public string PreviousPageUrl { get; set; }

    public string NextPageUrl { get; set; }

    public int FirstItem => Total == 0 ? 0 : ((Page - 1) * PageSize) + 1;

    public int LastItem => Math.Min(Page * PageSize, Total);

    public ProductBreadcrumbViewModel Breadcrumb { get; set; }

    public IShape FiltersShape { get; set; }

    public IShape SearchShape { get; set; }

    public IShape ToolbarShape { get; set; }

    public IShape ActiveFiltersShape { get; set; }

    public IShape ResultsShape { get; set; }

    public IShape PagerShape { get; set; }
}

/// <summary>
/// The model of every listing section shape: the listing page it belongs to.
/// </summary>
public class ProductListingShapeViewModel
{
    public ProductListingViewModel Listing { get; set; }
}

public class ProductFacetViewModel
{
    public string Key { get; set; }

    public string Label { get; set; }

    public ProductFacetKind Kind { get; set; }

    public bool Collapsed { get; set; }

    public int MaxVisible { get; set; }

    public IList<ProductFacetOptionViewModel> Options { get; set; } = [];

    public decimal? SelectedMin { get; set; }

    public decimal? SelectedMax { get; set; }

    public decimal? AvailableMin { get; set; }

    public decimal? AvailableMax { get; set; }

    /// <summary>
    /// The attribute's number format, such as <c>0</c> for a year, or <see langword="null"/> for the default.
    /// </summary>
    public string NumberFormat { get; set; }

    /// <summary>
    /// The attribute's unit, such as <c>mi</c>, or <see langword="null"/> when it has none.
    /// </summary>
    public string Suffix { get; set; }

    public bool IsPrice { get; set; }

    public string Currency { get; set; }

    /// <summary>
    /// The currency symbol shown beside a price range, such as <c>$</c>.
    /// </summary>
    public string CurrencySymbol { get; set; }

    public bool HasSelection { get; set; }

    /// <summary>
    /// How many options are picked, shown beside the filter's name.
    /// </summary>
    public int SelectedCount { get; set; }

    public string ClearUrl { get; set; }

    /// <summary>
    /// The name of the style the filter is drawn in, such as <c>Buttons</c> or <c>Swatches</c>. Empty for a list.
    /// </summary>
    public string Style { get; set; }

    /// <summary>
    /// Ready-made ranges offered above a range filter's inputs, when the filter asks for them.
    /// </summary>
    public IList<ProductFacetRangeViewModel> Ranges { get; set; } = [];
}

public class ProductFacetRangeViewModel
{
    public string Label { get; set; }

    public decimal? Min { get; set; }

    public decimal? Max { get; set; }

    public string Url { get; set; }

    public bool Selected { get; set; }
}

public class ProductFacetOptionViewModel
{
    public string Value { get; set; }

    public string Label { get; set; }

    public int Count { get; set; }

    public bool Selected { get; set; }

    public string Url { get; set; }

    /// <summary>
    /// The option's swatch color, safe to use in a style attribute, or <see langword="null"/> when it has none.
    /// </summary>
    public string Color { get; set; }
}

public class ProductActiveFilterViewModel
{
    public string Label { get; set; }

    public string RemoveUrl { get; set; }
}

public class ProductSortOptionViewModel
{
    public string Key { get; set; }

    public string Label { get; set; }

    public bool Selected { get; set; }
}

public class ProductPageLinkViewModel
{
    public int? Page { get; set; }

    public string Url { get; set; }

    public bool Current { get; set; }
}

public class ProductCardViewModel
{
    public ContentItem ContentItem { get; set; }

    public ProductSearchDocument Product { get; set; }

    public string Title { get; set; }

    public string Url { get; set; }

    public string ImageUrl { get; set; }

    public string ImageAspectRatio { get; set; }

    public IShape Price { get; set; }

    /// <summary>
    /// The saving against the compare-at price, in percent, when the product is on sale.
    /// </summary>
    public int? DiscountPercent { get; set; }

    public bool IsPurchasable { get; set; }

    /// <summary>
    /// Whether the card says if the product can be bought online. Only useful when a type mixes products that
    /// can and cannot be bought.
    /// </summary>
    public bool ShowAvailability { get; set; }

    public IList<string> Chips { get; set; } = [];

    public IList<ProductSpecificationViewModel> Specifications { get; set; } = [];

    /// <summary>
    /// What modules offer to do with the product from its card, such as subscribing: the product's
    /// <c>ProductAction</c> display.
    /// </summary>
    public IShape Actions { get; set; }
}

public class ProductPriceDisplayViewModel
{
    public ProductPriceDisplay Display { get; set; }

    public string Price { get; set; }

    public string CompareAtPrice { get; set; }

    public int? DiscountPercent { get; set; }

    public string OnRequestLabel { get; set; }

    /// <summary>
    /// How often the price is charged, such as "/ month", or <see langword="null"/> for a one-time price.
    /// </summary>
    public string Period { get; set; }
}

public class ProductSpecificationViewModel
{
    public string Key { get; set; }

    public string Label { get; set; }

    public string Value { get; set; }
}

public class ProductBreadcrumbViewModel
{
    /// <summary>
    /// The stable trail name, used for CSS classes and shape alternates of Orchard Core's breadcrumb shapes.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// The current page, the last and unlinked node of the trail.
    /// </summary>
    public string Title { get; set; }

    public IList<ProductBreadcrumbItemViewModel> Items { get; set; } = [];
}

public class ProductBreadcrumbItemViewModel
{
    /// <summary>
    /// A stable identifier used for shape alternates, not an HTML id.
    /// </summary>
    public string Id { get; set; }

    public string Label { get; set; }

    public string Url { get; set; }
}

/// <summary>
/// Everything a product's page shows. Each section is its own shape built from this model.
/// </summary>
public class ProductDetailsViewModel
{
    public ContentItem ContentItem { get; set; }

    public string Title { get; set; }

    public IShape Price { get; set; }

    public bool IsPurchasable { get; set; }

    public string Sku { get; set; }

    public string InquiryLabel { get; set; }

    public string InquiryUrl { get; set; }

    public IList<string> Highlights { get; set; } = [];

    public IList<ProductSpecificationViewModel> Specifications { get; set; } = [];

    public IList<ProductImageViewModel> Images { get; set; } = [];

    public ProductBreadcrumbViewModel Breadcrumb { get; set; }

    public IShape GalleryShape { get; set; }

    public IShape ActionsShape { get; set; }

    /// <summary>
    /// What modules offer to do with the product, such as subscribing: the product's <c>ProductAction</c>
    /// display, shown inside <see cref="ActionsShape"/>.
    /// </summary>
    public IShape ProductActions { get; set; }
}

/// <summary>
/// The model of every product page section shape: the product it belongs to.
/// </summary>
public class ProductDetailsShapeViewModel
{
    public ProductDetailsViewModel Product { get; set; }
}

public class ProductImageViewModel
{
    public string Url { get; set; }

    public string ThumbnailUrl { get; set; }

    public string Alt { get; set; }
}

public class ProductPageSettingsViewModel
{
    public bool ShowDetails { get; set; }

    public string InquiryLabel { get; set; }

    public string InquiryUrl { get; set; }
}
