namespace CrestApps.OrchardCore.AI.Deployments.ViewModels;

public class AIToolInstanceDeploymentStepViewModel
{
    public bool IncludeAll { get; set; }

    public AIToolInstanceEntryViewModel[] Instances { get; set; }
}
