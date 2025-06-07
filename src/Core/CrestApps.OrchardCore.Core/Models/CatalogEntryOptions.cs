using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Core.Models;

public class CatalogEntryOptions<TOptions>
{
    public string Search { get; set; }

    public TOptions BulkAction { get; set; }

    [BindNever]
    public IList<SelectListItem> BulkActions { get; set; }
}

public class CatalogEntryOptions : CatalogEntryOptions<CatalogEntryAction>;
