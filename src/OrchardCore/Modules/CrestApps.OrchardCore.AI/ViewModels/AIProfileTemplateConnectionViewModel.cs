using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.ViewModels;

public class AIProfileTemplateConnectionViewModel
{
    public string OrchestratorName { get; set; }

    public string InitialResponseHandlerName { get; set; }

    [BindNever]
    public IList<SelectListItem> Orchestrators { get; set; }

    [BindNever]
    public IList<SelectListItem> ResponseHandlers { get; set; }
}
