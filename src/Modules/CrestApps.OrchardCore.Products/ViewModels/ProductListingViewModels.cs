using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Core.Models;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Products.ViewModels;

public class ProductListingViewModel
{
    public ContentItem ContentItem { get; set; }

    public ProductListingPart Part { get; set; }

    public string FormAction { get; set; }

    public bool EnableKeywordSearch { get; set; }

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

    public bool HasSelection { get; set; }

    public string ClearUrl { get; set; }
}

public class ProductFacetOptionViewModel
{
    public string Value { get; set; }

    public string Label { get; set; }

    public int Count { get; set; }

    public bool Selected { get; set; }

    public string Url { get; set; }
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

    public string ImageUrl { get; set; }

    public IShape Price { get; set; }

    public bool IsPurchasable { get; set; }

    public IList<string> Chips { get; set; } = [];

    public IList<ProductSpecificationViewModel> Specifications { get; set; } = [];
}

public class ProductPriceDisplayViewModel
{
    public ProductPriceDisplay Display { get; set; }

    public string Price { get; set; }

    public string CompareAtPrice { get; set; }

    public int? DiscountPercent { get; set; }

    public string OnRequestLabel { get; set; }
}

public class ProductSpecificationViewModel
{
    public string Key { get; set; }

    public string Label { get; set; }

    public string Value { get; set; }
}

public class ProductDetailsViewModel
{
    public ContentItem ContentItem { get; set; }

    public IShape Price { get; set; }

    public bool IsPurchasable { get; set; }

    public string Sku { get; set; }

    public string InquiryLabel { get; set; }

    public string InquiryUrl { get; set; }

    public IList<ProductSpecificationViewModel> Specifications { get; set; } = [];
}

public class ProductPageSettingsViewModel
{
    public bool ShowDetails { get; set; }

    public string InquiryLabel { get; set; }

    public string InquiryUrl { get; set; }
}
