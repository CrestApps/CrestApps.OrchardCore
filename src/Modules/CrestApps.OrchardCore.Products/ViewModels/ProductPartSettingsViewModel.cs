using CrestApps.OrchardCore.Products.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Products.ViewModels;

public class ProductPartSettingsViewModel
{
    public ProductType Type { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> Types { get; set; }
}
