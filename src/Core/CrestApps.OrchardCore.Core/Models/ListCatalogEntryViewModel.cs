using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Core.Models;

/// <summary>
/// Represents the view model for list catalog entry.
/// </summary>
public class ListCatalogEntryViewModel
{
    /// <summary>
    /// Gets or sets the options.
    /// </summary>
    public CatalogEntryOptions Options { get; set; }

    /// <summary>
    /// Gets or sets the pager shape used to render pagination controls.
    /// Downstream consumers should cast to the concrete pager type.
    /// </summary>
    public object Pager { get; set; }

    /// <summary>
    /// Gets or sets the items, pager and region shapes the <c>admin-list</c> tag renders, in the
    /// layout (List or Grid) the site or the user picked.
    /// </summary>
    [BindNever]
    public AdminListContext List { get; set; }
}

/// <summary>
/// Represents the view model for list catalog entry.
/// </summary>
public class ListCatalogEntryViewModel<T> : ListCatalogEntryViewModel
{
    /// <summary>
    /// Gets or sets the models.
    /// </summary>
    public IList<T> Models { get; set; }
}
