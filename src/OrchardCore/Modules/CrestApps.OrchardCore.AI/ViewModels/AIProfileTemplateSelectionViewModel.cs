using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.ViewModels;

public class AIProfileTemplateSelectionViewModel
{
    public string TemplateId { get; set; }

    public string Source { get; set; }
    public IList<SelectListItem> Templates { get; set; } = [];
}
