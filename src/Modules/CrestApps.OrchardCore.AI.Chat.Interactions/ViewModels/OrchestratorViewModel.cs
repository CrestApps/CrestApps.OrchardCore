using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;

public class OrchestratorViewModel
{
    public string OrchestratorName { get; set; }

    [BindNever]
    public IList<SelectListItem> Orchestrators { get; set; }
}
