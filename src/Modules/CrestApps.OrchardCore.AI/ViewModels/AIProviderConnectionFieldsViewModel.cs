using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.ViewModels;

public class AIProviderConnectionFieldsViewModel
{
    public string DisplayText { get; set; }

    public string Name { get; set; }

    public bool IsDefault { get; set; }

    public string DefaultDeploymentName { get; set; }

    public AIProviderConnectionType? Type { get; set; }

    [BindNever]
    public bool IsNew { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> Types { get; set; }
}
