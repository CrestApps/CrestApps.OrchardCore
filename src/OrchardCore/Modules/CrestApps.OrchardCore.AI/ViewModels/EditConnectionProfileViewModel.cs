using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.ViewModels;

public class EditConnectionProfileViewModel
{
    public string OrchestratorName { get; set; }
    [BindNever]
    public IList<SelectListItem> Orchestrators { get; set; }
}
