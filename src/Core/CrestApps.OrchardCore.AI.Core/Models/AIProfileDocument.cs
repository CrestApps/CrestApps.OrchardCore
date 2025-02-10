using CrestApps.OrchardCore.AI.Models;
using OrchardCore.Data.Documents;

namespace CrestApps.OrchardCore.AI.Core.Models;

public sealed class AIProfileDocument : Document
{
    public Dictionary<string, AIProfile> Profiles { get; set; } = [];
}
