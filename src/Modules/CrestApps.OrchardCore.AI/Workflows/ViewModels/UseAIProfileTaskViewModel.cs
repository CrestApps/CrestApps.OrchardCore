using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Workflows.ViewModels;

/// <summary>
/// Edits the Use AI Profile task.
/// </summary>
public class UseAIProfileTaskViewModel
{
    /// <summary>
    /// Gets or sets the AI profile to answer with.
    /// </summary>
    public string ProfileId { get; set; }

    /// <summary>
    /// Gets or sets the AI profiles to choose from.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> Profiles { get; set; } = [];
}
