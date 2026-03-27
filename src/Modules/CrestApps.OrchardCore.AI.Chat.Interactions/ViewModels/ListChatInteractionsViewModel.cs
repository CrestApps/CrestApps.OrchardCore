using CrestApps.OrchardCore.AI.Models;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;

public class ListChatInteractionsViewModel
{
    public IEnumerable<ChatInteraction> Interactions { get; set; }

    public IShape Pager { get; set; }

    public ChatInteractionListOptions Options { get; set; }

    public IShape Header { get; set; }
}
