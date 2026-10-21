using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Products.ViewModels;

/// <summary>
/// The merchant's upsell and cross-sell pickers on a product's editor.
/// </summary>
public class ProductRecommendationsEditViewModel
{
    public string[] UpsellIds { get; set; } = [];

    public string[] CrossSellIds { get; set; } = [];

    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public string SearchEndpoint { get; set; }

    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public string InitialUpsellsJson { get; set; }

    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public string InitialCrossSellsJson { get; set; }
}

/// <summary>
/// A strip of product cards under a heading, such as "You may also like".
/// </summary>
public class ProductCardStripViewModel
{
    public string Heading { get; set; }

    public IList<IShape> Cards { get; set; } = [];
}

/// <summary>
/// The products a shopper compares, as their listing cards with their specifications.
/// </summary>
public class ProductCompareViewModel
{
    public IList<IShape> Cards { get; set; } = [];
}

/// <summary>
/// The place on a product's page where the shopper's recently viewed products load.
/// </summary>
public class ProductRecentlyViewedViewModel
{
    public string ContentItemId { get; set; }
}

/// <summary>
/// The picker for the products a grouped product shows.
/// </summary>
public class ProductGroupEditViewModel
{
    public List<string> ProductIds { get; set; } = [];

    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public string SearchEndpoint { get; set; }

    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public string InitialProductsJson { get; set; }
}

/// <summary>
/// The rows of a bundle's editor: a product and how many of it each bundle holds.
/// </summary>
public class ProductBundleEditViewModel
{
    public List<ProductBundleItemViewModel> Items { get; set; } = [];

    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public string SearchEndpoint { get; set; }
}

public class ProductBundleItemViewModel
{
    public string ContentItemId { get; set; }

    public int? Quantity { get; set; }

    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public string InitialJson { get; set; }
}

/// <summary>
/// What's in a bundle, on its page.
/// </summary>
public class ProductBundleContentsViewModel
{
    public IList<ProductBundleContentViewModel> Items { get; set; } = [];
}

public class ProductBundleContentViewModel
{
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the product, or <see langword="null"/> when it's no longer published.
    /// </summary>
    public global::OrchardCore.ContentManagement.ContentItem ContentItem { get; set; }

    public int Quantity { get; set; }
}
