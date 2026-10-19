using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Orders.ViewModels;

// Not sealed: Orchard Core derives a proxy from a view model it initializes, and a sealed one renders nothing.
public class CouponItemLimitsViewModel
{
    public string[] ProductIds { get; set; } = [];

    public string[] CategoryIds { get; set; } = [];

    public string[] BuyProductIds { get; set; } = [];

    public string[] BuyCategoryIds { get; set; } = [];

    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public string InitialBuyProductsJson { get; set; } = "[]";

    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public string InitialBuyCategoriesJson { get; set; } = "[]";

    [BindNever]
    public string ProductSearchEndpoint { get; set; }

    [BindNever]
    public string CategorySearchEndpoint { get; set; }

    [BindNever]
    public string InitialProductsJson { get; set; } = "[]";

    [BindNever]
    public string InitialCategoriesJson { get; set; } = "[]";
}
