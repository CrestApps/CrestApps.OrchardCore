using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.ViewModels;

public class AIProviderConnectionFieldsViewModel
{
    public string DisplayText { get; set; }

    public string Name { get; set; }

    public bool IsDefault { get; set; }

    public string DefaultDeploymentName { get; set; }

    public string DefaultEmbeddingDeploymentName { get; set; }

    public string DefaultSpeechToTextDeploymentName { get; set; }

    public string DefaultIntentDeploymentName { get; set; }

    public string DefaultImagesDeploymentName { get; set; }

    [BindNever]
    public bool IsNew { get; set; }
}
