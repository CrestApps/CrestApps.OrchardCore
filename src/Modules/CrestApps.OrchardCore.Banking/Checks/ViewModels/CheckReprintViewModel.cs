using CrestApps.OrchardCore.Banking.Checks.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

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
}
