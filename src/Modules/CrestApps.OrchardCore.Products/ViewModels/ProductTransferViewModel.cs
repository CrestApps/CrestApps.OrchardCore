using CrestApps.OrchardCore.Products.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Products.ViewModels;

public class ProductTransferViewModel
{
    public string ContentType { get; set; }

    public bool UpdateExisting { get; set; } = true;

    public bool Publish { get; set; }

    [BindNever]
    public IList<SelectListItem> ContentTypes { get; set; } = [];

    /// <summary>
    /// Gets or sets the format the last import was read as, or <see langword="null"/> when it wasn't recognized.
    /// </summary>
    [BindNever]
    public string Format { get; set; }

    [BindNever]
    public ProductImportResult Result { get; set; }

    [BindNever]
    public IList<string> Problems { get; set; } = [];
}
