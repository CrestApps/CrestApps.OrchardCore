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
