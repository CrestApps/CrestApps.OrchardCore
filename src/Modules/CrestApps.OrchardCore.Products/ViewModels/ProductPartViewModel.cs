using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Products.ViewModels;

public class ProductPartViewModel
{
    public decimal? Price { get; set; }

    public string Currency { get; set; }

    public string Sku { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> Currencies { get; set; }
}
