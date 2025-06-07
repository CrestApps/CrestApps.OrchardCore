using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

public class DisplayAIChatWidgetViewModel
{
    [BindNever]
    public IEnumerable<AIChatSession> Sessions { get; set; }
}
