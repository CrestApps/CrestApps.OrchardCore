using CrestApps.Core.AI.Models;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;

public class EditChatInteractionToolsViewModel
{
    public Dictionary<string, ToolEntry[]> Tools { get; set; }
}
