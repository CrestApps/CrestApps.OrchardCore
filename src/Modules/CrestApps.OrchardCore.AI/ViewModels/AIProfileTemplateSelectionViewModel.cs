using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.ViewModels;

/// <summary>
/// Represents the view model for AI profile template selection.
/// </summary>
public class AIProfileTemplateSelectionViewModel
{
    /// <summary>
    /// Gets or sets the template id.
    /// </summary>
    public string TemplateId { get; set; }

    /// <summary>
    /// Gets or sets the source.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the templates.
    /// </summary>
    public IList<SelectListItem> Templates { get; set; } = [];
}
