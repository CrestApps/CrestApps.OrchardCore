using CrestApps.OrchardCore.OpenAI.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.OpenAI.ViewModels.Sessions;

public class ChatSessionCapsuleViewModel
{
    public OpenAIChatSession Session { get; set; }

    [BindNever]
    public bool IsNew { get; set; }
}
