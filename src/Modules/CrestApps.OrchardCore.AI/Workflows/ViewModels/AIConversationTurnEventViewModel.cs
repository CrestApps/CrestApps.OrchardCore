using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Workflows.ViewModels;

/// <summary>
/// Edits the AI Conversation Turn event.
/// </summary>
public class AIConversationTurnEventViewModel
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
    /// Gets or sets whose turns start the workflow, or empty for both.
    /// </summary>
    public string Role { get; set; }

    /// <summary>
    /// Gets or sets the channels to choose from.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> Channels { get; set; } = [];

    /// <summary>
    /// Gets or sets the roles to choose from.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> Roles { get; set; } = [];

    /// <summary>
    /// Gets or sets the AI profiles to choose from.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> Profiles { get; set; } = [];
}
