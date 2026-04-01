using CrestApps.AI.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;

public class ChatInteractionChatModeSettingsViewModel
{
    public ChatMode ChatMode { get; set; }
    [BindNever]
    public IEnumerable<SelectListItem> AvailableModes { get; set; }
}
