using CrestApps.Core.AI.Models;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;

/// <summary>
/// Represents the view model for edit chat interaction tools.
/// </summary>
public class EditChatInteractionToolsViewModel
{
    /// <summary>
    /// Gets or sets the tools.
    /// </summary>
    public Dictionary<string, ToolEntry[]> Tools { get; set; }
}
