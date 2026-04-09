using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Workflows.ViewModels;

public class AIChatSessionPostProcessedEventViewModel
{
    public string ProfileId { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> Profiles { get; set; }
}
