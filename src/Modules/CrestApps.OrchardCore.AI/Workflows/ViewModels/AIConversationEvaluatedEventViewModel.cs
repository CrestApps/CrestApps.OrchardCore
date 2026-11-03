using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Workflows.ViewModels;

/// <summary>
/// Edits the AI Conversation Evaluated event.
/// </summary>
public class AIConversationEvaluatedEventViewModel
{
    /// <summary>
    /// Gets or sets the AI profile the workflow is for.
    /// </summary>
    public string ProfileId { get; set; }

    /// <summary>
    /// Gets or sets whether only failed evaluations start the workflow.
    /// </summary>
    public bool OnlyFailures { get; set; }

    /// <summary>
    /// Gets or sets the AI profiles to choose from.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> Profiles { get; set; } = [];
}
