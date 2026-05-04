using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.ViewModels;

/// <summary>
/// Represents the view model for AI profile template connection.
/// </summary>
public class AIProfileTemplateConnectionViewModel
{
    /// <summary>
    /// Gets or sets the orchestrator name.
    /// </summary>
    public string OrchestratorName { get; set; }

    /// <summary>
    /// Gets or sets the initial response handler name.
    /// </summary>
    public string InitialResponseHandlerName { get; set; }

    /// <summary>
    /// Gets or sets the orchestrators.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> Orchestrators { get; set; }

    /// <summary>
    /// Gets or sets the response handlers.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> ResponseHandlers { get; set; }
}
