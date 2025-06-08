using CrestApps.OrchardCore.AI.Models;
using OrchardCore.Data.Documents;

namespace CrestApps.OrchardCore.AI.Core.Models;

[Obsolete("This class will be removed before the v1 is released.")]
public sealed class AIDeploymentDocument : Document
{
    public Dictionary<string, AIDeployment> Deployments { get; set; } = [];
}
