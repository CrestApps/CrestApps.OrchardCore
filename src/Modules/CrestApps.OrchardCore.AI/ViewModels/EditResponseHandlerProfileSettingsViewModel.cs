using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.ViewModels;

/// <summary>
/// Represents the view model for edit response handler profile settings.
/// </summary>
public class EditResponseHandlerProfileSettingsViewModel
{
    /// <summary>
    /// Gets or sets the initial response handler name.
    /// </summary>
    public string InitialResponseHandlerName { get; set; }

    /// <summary>
    /// Gets or sets the response handlers.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> ResponseHandlers { get; set; }
}
