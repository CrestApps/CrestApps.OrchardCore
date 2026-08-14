using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Core.Models;

/// <summary>
/// Represents the catalog entry options.
/// </summary>
public class CatalogEntryOptions<TOptions>
{
    /// <summary>
    /// Gets or sets the search.
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// Gets or sets the bulk action.
    /// </summary>
    public TOptions BulkAction { get; set; }

    /// <summary>
    /// Gets or sets the bulk actions.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> BulkActions { get; set; }
}

/// <summary>
/// Represents the catalog entry options.
/// </summary>
public class CatalogEntryOptions : CatalogEntryOptions<CatalogEntryAction>;
