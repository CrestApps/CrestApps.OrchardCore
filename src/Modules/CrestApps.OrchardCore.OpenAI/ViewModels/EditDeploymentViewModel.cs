using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.OpenAI.ViewModels;

public class EditDeploymentViewModel
{
    public string Name { get; set; }

    public string ConnectionName { get; set; }

    [BindNever]
    public bool IsNew { get; set; }

    [BindNever]
    public IList<SelectListItem> Connections { get; set; }
}
