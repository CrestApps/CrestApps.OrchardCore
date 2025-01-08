using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.OpenAI.Models;

public class OpenAIDeploymentOptions
{
    public string Search { get; set; }

    public OpenAIDeploymentAction BulkAction { get; set; }

    [BindNever]
    public List<SelectListItem> BulkActions { get; set; }
}
