using CrestApps.OrchardCore.OpenAI.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.OpenAI.ViewModels;

public class DisplayOpenAIChatWidgetViewModel
{
    [BindNever]
    public IEnumerable<OpenAIChatSession> Sessions { get; set; }
}
