using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;

/// <summary>
/// Represents the view model for orchestrator.
/// </summary>
public class OrchestratorViewModel
{
    /// <summary>
    /// Gets or sets the orchestrator name.
    /// </summary>
    public string OrchestratorName { get; set; }

    /// <summary>
    /// Gets or sets the orchestrators.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> Orchestrators { get; set; }
}
