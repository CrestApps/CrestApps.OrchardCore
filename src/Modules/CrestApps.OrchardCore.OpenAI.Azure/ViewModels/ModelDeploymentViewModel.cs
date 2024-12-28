using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.OpenAI.Azure.ViewModels;

public class ModelDeploymentViewModel
{
    public string AIModel { get; set; }

    [BindNever]
    public IList<SelectListItem> AIModels { get; set; }
}
