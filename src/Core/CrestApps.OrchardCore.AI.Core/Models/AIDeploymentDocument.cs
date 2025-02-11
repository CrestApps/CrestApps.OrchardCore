using CrestApps.OrchardCore.AI.Models;
using OrchardCore.Data.Documents;

namespace CrestApps.OrchardCore.AI.Core.Models;

public sealed class AIDeploymentDocument : Document
{
    public Dictionary<string, AIDeployment> Deployments { get; set; } = [];
}
