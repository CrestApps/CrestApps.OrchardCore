namespace CrestApps.OrchardCore.AI.Models;

public class AIDeploymentResult
{
    public int Count { get; set; }

    public IEnumerable<AIDeployment> Deployments { get; set; }
}
