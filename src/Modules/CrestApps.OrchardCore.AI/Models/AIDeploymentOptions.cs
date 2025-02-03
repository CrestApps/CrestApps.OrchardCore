using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Models;

public class AIDeploymentOptions
{
    public string Search { get; set; }

    public AIDeploymentAction BulkAction { get; set; }

    [BindNever]
    public List<SelectListItem> BulkActions { get; set; }
}
