using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.ViewModels;

public class AIProviderConnectionFieldsViewModel
{
    public string DisplayText { get; set; }

    public string Name { get; set; }

    [Obsolete("Deployment names are now managed through AIDeployment entities. Retained for backward compatibility.")]
    public string ChatDeploymentName { get; set; }

    [Obsolete("Deployment names are now managed through AIDeployment entities. Retained for backward compatibility.")]
    public string EmbeddingDeploymentName { get; set; }

    [Obsolete("Deployment names are now managed through AIDeployment entities. Retained for backward compatibility.")]
    public string ImagesDeploymentName { get; set; }

    [Obsolete("Deployment names are now managed through AIDeployment entities. Retained for backward compatibility.")]
    public string UtilityDeploymentName { get; set; }

    [BindNever]
    public bool IsNew { get; set; }
}
