namespace CrestApps.OrchardCore.OpenAI.Models;

public class OpenAIDeploymentResult
{
    public int Count { get; set; }

    public IEnumerable<OpenAIDeployment> Deployments { get; set; }
}
