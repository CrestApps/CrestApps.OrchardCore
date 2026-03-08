using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.ViewModels;

public class AIProviderConnectionFieldsViewModel
{
    public string DisplayText { get; set; }

    public string Name { get; set; }

    public bool IsDefault { get; set; }

    public string ChatDeploymentName { get; set; }

    public string EmbeddingDeploymentName { get; set; }

    public string ImagesDeploymentName { get; set; }

    public string UtilityDeploymentName { get; set; }

    [BindNever]
    public bool IsNew { get; set; }
}
