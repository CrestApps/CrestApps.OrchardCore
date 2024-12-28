using CrestApps.OrchardCore.OpenAI.Models;
using OrchardCore.Data.Documents;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Models;

public sealed class ModelDeploymentDocument : Document
{
    public Dictionary<string, ModelDeployment> Deployments { get; set; } = [];
}
