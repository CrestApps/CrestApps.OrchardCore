using CrestApps.Core.AI.Models;

namespace CrestApps.OrchardCore.AI.ViewModels;

/// <summary>
/// Represents the view model for edit profile agents.
/// </summary>
public class EditProfileAgentsViewModel
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
