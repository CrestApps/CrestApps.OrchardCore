using CrestApps.OrchardCore.Products.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Products.ViewModels;

public class ProductPartViewModel
{
    public decimal? Price { get; set; }

    public decimal? CompareAtPrice { get; set; }

    public string Currency { get; set; }

    public string Sku { get; set; }

    public bool IsPurchasable { get; set; }

    public ProductPriceDisplay? PriceDisplay { get; set; }

    public bool Featured { get; set; }

    public int? Position { get; set; }

    [BindNever]
    public bool ShowPurchasable { get; set; }

    [BindNever]
    public bool ShowPriceDisplay { get; set; }

    [BindNever]
    public bool PriceRequired { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> Currencies { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> PriceDisplays { get; set; }
}
