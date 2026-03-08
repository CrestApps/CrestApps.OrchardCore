namespace CrestApps.OrchardCore.AI.Deployments.ViewModels;

public class AIProviderConnectionDeploymentStepViewModel
{
    public bool IncludeAll { get; set; }

    public AIProviderConnectionEntryViewModel[] Connections { get; set; }
}
