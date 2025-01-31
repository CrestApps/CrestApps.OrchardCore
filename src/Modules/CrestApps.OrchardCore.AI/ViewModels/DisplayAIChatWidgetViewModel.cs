using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.ViewModels;

public class DisplayAIChatWidgetViewModel
{
    [BindNever]
    public IEnumerable<AIChatSession> Sessions { get; set; }
}
