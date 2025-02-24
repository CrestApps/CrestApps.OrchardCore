using CrestApps.OrchardCore.AI.Models;
using OrchardCore.Data.Documents;

namespace CrestApps.OrchardCore.AI.Core.Models;

public sealed class AIToolInstancesDocument : Document
{
    public Dictionary<string, AIToolInstance> Instances { get; set; } = [];
}
