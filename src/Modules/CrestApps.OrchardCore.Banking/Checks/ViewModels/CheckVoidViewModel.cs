using CrestApps.OrchardCore.Banking.Checks.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Banking.Checks.ViewModels;

/// <summary>
/// The page that voids a check on a date, with the reason.
/// </summary>
public class CheckVoidViewModel
{
    /// <summary>
    /// Gets or sets why the check is voided.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the void date, as yyyy-MM-dd.
    /// </summary>
    public string VoidedOn { get; set; }

    /// <summary>
    /// Gets or sets the check.
    /// </summary>
    [BindNever]
    public CheckRecord Check { get; set; }

    /// <summary>
    /// Gets or sets the editor of the page's fields, built by display drivers so they are grouped in cards by placement.
    /// </summary>
    [BindNever]
    public IShape Editor { get; set; }
}
