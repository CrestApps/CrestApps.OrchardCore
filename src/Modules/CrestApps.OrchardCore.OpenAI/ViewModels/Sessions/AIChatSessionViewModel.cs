using CrestApps.OrchardCore.OpenAI.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.OpenAI.ViewModels.Sessions;

public class AIChatSessionViewModel
{
    public AIChatSession Session { get; set; }

    [BindNever]
    public bool IsNew { get; set; }
}
