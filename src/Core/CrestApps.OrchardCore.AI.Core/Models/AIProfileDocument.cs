using CrestApps.OrchardCore.AI.Models;
using OrchardCore.Data.Documents;

namespace CrestApps.OrchardCore.AI.Core.Models;

[Obsolete("This class will be removed before the v1 is released.")]
public sealed class AIProfileDocument : Document
{
    public Dictionary<string, AIProfile> Profiles { get; set; } = [];
}
