namespace CrestApps.OrchardCore.AI.Deployments.ViewModels;

public class AIDataSourceDeploymentStepViewModel
{
    public bool IncludeAll { get; set; }

    public AIDataSourceEntryViewModel[] DataSources { get; set; }
}
