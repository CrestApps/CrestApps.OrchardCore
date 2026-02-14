using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.ViewModels;

public class EditConnectionProfileViewModel
{
    public string ConnectionName { get; set; }

    public string OrchestratorName { get; set; }

    [BindNever]
    public IList<SelectListItem> ConnectionNames { get; set; }

    [BindNever]
    public IList<SelectListItem> Orchestrators { get; set; }
}
