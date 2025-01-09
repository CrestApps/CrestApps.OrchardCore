using CrestApps.OrchardCore.OpenAI.Models;
using OrchardCore.Data.Documents;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Models;

public sealed class OpenAIDeploymentDocument : Document
{
    public Dictionary<string, OpenAIDeployment> Deployments { get; set; } = [];
}
