using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.ViewModels;

/// <summary>
/// Represents the view model for edit deployment connection.
/// </summary>
public class EditDeploymentConnectionViewModel
{
    /// <summary>
    /// Gets or sets the connection name.
    /// </summary>
    public string ConnectionName { get; set; }

    /// <summary>
    /// Gets or sets the connections.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> Connections { get; set; }
}
