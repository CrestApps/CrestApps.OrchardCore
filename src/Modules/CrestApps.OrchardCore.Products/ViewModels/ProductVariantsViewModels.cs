using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Products.ViewModels;

public class ProductVariantsPartViewModel
{
    public List<ProductOptionViewModel> Options { get; set; } = [];

    public List<ProductVariantViewModel> Variants { get; set; } = [];

    [BindNever]
    public string ProductSku { get; set; }

    [BindNever]
    public decimal? ProductPrice { get; set; }

    /// <summary>
    /// The media library paths of the product's own images, which a variant picks from.
    /// </summary>
    [BindNever]
    public IList<string> Images { get; set; } = [];
}

public class ProductOptionViewModel
{
    public string Name { get; set; }

    /// <summary>
    /// The option's values, separated by commas.
    /// </summary>
    public string Values { get; set; }
}

public class ProductVariantViewModel
{
    public string VariantId { get; set; }

    [BindNever]
    public string Title { get; set; }

    public string Sku { get; set; }

    public decimal? Price { get; set; }

    public decimal? CompareAtPrice { get; set; }

    public decimal? SalePrice { get; set; }

    public string ImagePath { get; set; }

    public bool Enabled { get; set; }
}

public class ProductVariantsPartSettingsViewModel
{
    public List<ProductVariantOptionSettingsViewModel> Options { get; set; } = [];
}

public class ProductVariantOptionSettingsViewModel
{
    public string Name { get; set; }

    public bool Filterable { get; set; }
}
