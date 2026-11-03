using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Workflows.ViewModels;

/// <summary>
/// Edits the AI Conversation Starting event.
/// </summary>
public class AIConversationStartingEventViewModel
{
    /// <summary>
    /// Gets or sets the channel the workflow is for, or empty for all.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets the AI profile the workflow is for, or empty for all.
    /// </summary>
    public string ProfileId { get; set; }

    /// <summary>
    /// Gets or sets the channels to choose from.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> Channels { get; set; } = [];

    /// <summary>
    /// Gets or sets the AI profiles to choose from.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> Profiles { get; set; } = [];
}
