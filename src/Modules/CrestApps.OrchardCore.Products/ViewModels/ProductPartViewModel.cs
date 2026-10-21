using CrestApps.OrchardCore.Products.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Products.ViewModels;

public class ProductPartViewModel
{
    public decimal? Price { get; set; }

    public decimal? CompareAtPrice { get; set; }

    public decimal? SalePrice { get; set; }

    /// <summary>
    /// Gets or sets when the sale starts, in the site's time zone.
    /// </summary>
    public DateTime? SaleStarts { get; set; }

    /// <summary>
    /// Gets or sets when the sale ends, in the site's time zone.
    /// </summary>
    public DateTime? SaleEnds { get; set; }

    public string ExternalUrl { get; set; }

    /// <summary>
    /// Gets or sets the products a grouped product shows, posted by the Grouped Products picker. A group is bought
    /// through them, so it needs no price of its own.
    /// </summary>
    public List<string> ProductIds { get; set; }

    public string ExternalButtonText { get; set; }

    public int? MinQuantity { get; set; }

    public int? MaxQuantity { get; set; }

    public int? QuantityStep { get; set; }

    /// <summary>
    /// Gets or sets the volume discount rows: the existing ones and empty ones to fill in.
    /// </summary>
    public List<ProductPriceTierViewModel> PriceTiers { get; set; } = [];

    /// <summary>
    /// Gets or sets the role discount rows: the existing ones and empty ones to fill in.
    /// </summary>
    public List<ProductRoleDiscountViewModel> RoleDiscounts { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> Roles { get; set; } = [];

    /// <summary>
    /// Gets or sets the add-on rows: the existing ones and empty ones to fill in.
    /// </summary>
    public List<ProductAddOnViewModel> AddOns { get; set; } = [];

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

public class ProductPriceTierViewModel
{
    public int? MinQuantity { get; set; }

    public decimal? PercentOff { get; set; }
}

public class ProductRoleDiscountViewModel
{
    public string Role { get; set; }

    public decimal? PercentOff { get; set; }
}

public class ProductAddOnViewModel
{
    public string AddOnId { get; set; }

    public string Label { get; set; }

    public CrestApps.OrchardCore.Products.Core.Models.ProductAddOnKind Kind { get; set; }

    public decimal? Price { get; set; }

    public CrestApps.OrchardCore.Products.Core.Models.ProductAddOnPriceBasis PriceBasis { get; set; }

    public bool Required { get; set; }
}
