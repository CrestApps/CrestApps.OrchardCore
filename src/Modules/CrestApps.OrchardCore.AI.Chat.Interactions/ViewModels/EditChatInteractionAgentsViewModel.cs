using CrestApps.Core.AI.Models;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;

/// <summary>
/// Represents the view model for edit chat interaction agents.
/// </summary>
public class EditChatInteractionAgentsViewModel
{
    /// <summary>
    /// Gets or sets the agents.
    /// </summary>
    public ToolEntry[] Agents { get; set; }

    /// <summary>
    /// Gets or sets the always available agent count.
    /// </summary>
    public int AlwaysAvailableAgentCount { get; set; }
}
