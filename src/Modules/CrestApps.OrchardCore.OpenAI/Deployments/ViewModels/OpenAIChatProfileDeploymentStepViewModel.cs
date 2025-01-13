namespace CrestApps.OrchardCore.OpenAI.Deployments.ViewModels;

public class OpenAIChatProfileDeploymentStepViewModel
{
    public bool IncludeAll { get; set; }

    public string[] ProfileNames { get; set; }

    public string[] AllProfileNames { get; set; }
}
