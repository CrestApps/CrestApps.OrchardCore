using CrestApps.OrchardCore.Banking.Checks.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Banking.Checks.ViewModels;

/// <summary>
/// The page that edits a check waiting to print: the check's editor, built by the display drivers of
/// <see cref="CheckRecord"/>, and why it changes.
/// </summary>
public class CheckEditViewModel
{
    /// <summary>
    /// Gets or sets why the check changes.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the check as it is saved.
    /// </summary>
    [BindNever]
    public CheckRecord Check { get; set; }

    /// <summary>
    /// Gets or sets the editor shape.
    /// </summary>
    [BindNever]
    public object Editor { get; set; }
}
