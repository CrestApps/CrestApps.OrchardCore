using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;

public class EditChatInteractionDocumentsViewModel
{
    public string ItemId { get; set; }

    public IList<ChatInteractionDocument> Documents { get; set; } = [];
}
