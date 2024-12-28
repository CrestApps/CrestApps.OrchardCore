namespace CrestApps.OrchardCore.OpenAI.Models;

public class ModelDeploymentResult
{
    public int Count { get; set; }

    public IEnumerable<ModelDeployment> Deployments { get; set; }
}
