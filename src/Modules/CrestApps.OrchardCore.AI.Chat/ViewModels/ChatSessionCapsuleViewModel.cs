using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

public class ChatSessionCapsuleViewModel
{
    public AIChatSession Session { get; set; }

    public AIProfile Profile { get; set; }

    public CustomChatSession CustomChatSession { get; set; }

    [BindNever]
    public bool IsNew { get; set; }
}
