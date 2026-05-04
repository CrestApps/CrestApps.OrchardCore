using CrestApps.Core.AI.Claude.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Chat.Claude.ViewModels;

/// <summary>
/// Represents the view model for edit claude profile.
/// </summary>
public class EditClaudeProfileViewModel
{
    /// <summary>
    /// Gets or sets the claude model.
    /// </summary>
    public string ClaudeModel { get; set; }

    /// <summary>
    /// Gets or sets the claude effort level.
    /// </summary>
    public ClaudeEffortLevel ClaudeEffortLevel { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether is claude configured.
    /// </summary>
    [BindNever]
    public bool IsClaudeConfigured { get; set; }

    /// <summary>
    /// Gets or sets the available models.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> AvailableModels { get; set; }
}
