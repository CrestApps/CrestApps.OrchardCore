using CrestApps.AI.Models;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;

public class EditChatInteractionAgentsViewModel
{
    public ToolEntry[] Agents { get; set; }

    public int AlwaysAvailableAgentCount { get; set; }
}
