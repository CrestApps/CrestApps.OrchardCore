using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.ViewModels;

/// <summary>
/// Represents the view model for edit deployment.
/// </summary>
public class EditDeploymentViewModel
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the model name.
    /// </summary>
    public string ModelName { get; set; }

    /// <summary>
    /// Gets or sets the selected types.
    /// </summary>
    public string[] SelectedTypes { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether is new.
    /// </summary>
    [BindNever]
    public bool IsNew { get; set; }

    /// <summary>
    /// Gets or sets the types.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> Types { get; set; }
}
