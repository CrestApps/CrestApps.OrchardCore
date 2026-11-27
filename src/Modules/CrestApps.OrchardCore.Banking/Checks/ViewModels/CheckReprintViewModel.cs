using CrestApps.OrchardCore.Banking.Checks.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Banking.Checks.ViewModels;

/// <summary>
/// The page that prints a printed check again, with the reason.
/// </summary>
public class CheckReprintViewModel
{
    /// <summary>
    /// Gets or sets the layout to print with.
    /// </summary>
    public string LayoutId { get; set; }

    /// <summary>
    /// Gets or sets why the check is printed again.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the check.
    /// </summary>
    [BindNever]
    public CheckRecord Check { get; set; }

    /// <summary>
    /// Gets or sets the layouts.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> LayoutOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the editor of the page's fields, built by display drivers so they are grouped in cards by placement.
    /// </summary>
    [BindNever]
    public IShape Editor { get; set; }
}
