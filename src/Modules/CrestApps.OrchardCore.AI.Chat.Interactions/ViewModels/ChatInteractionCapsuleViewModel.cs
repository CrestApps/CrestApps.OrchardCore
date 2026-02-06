using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;

public class ChatInteractionCapsuleViewModel
{
    public ChatInteraction Interaction { get; set; }

    [BindNever]
    public IReadOnlyCollection<ChatInteractionPrompt> Prompts { get; set; } = [];

    [BindNever]
    public bool IsNew { get; set; }
}
