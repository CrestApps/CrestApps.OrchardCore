using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.ViewModels;

public class EditDeploymentConnectionViewModel
{
    public string ConnectionName { get; set; }

    [BindNever]
    public IList<SelectListItem> Connections { get; set; }
}
