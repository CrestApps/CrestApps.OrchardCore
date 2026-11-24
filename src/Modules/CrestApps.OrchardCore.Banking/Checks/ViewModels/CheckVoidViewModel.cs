using CrestApps.OrchardCore.Banking.Checks.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

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
}
