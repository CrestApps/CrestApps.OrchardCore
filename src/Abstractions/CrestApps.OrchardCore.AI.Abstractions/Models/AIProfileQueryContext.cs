using CrestApps.OrchardCore.Models;

namespace CrestApps.OrchardCore.AI.Models;

public sealed class AIProfileQueryContext : QueryContext
{
    public bool IsListableOnly { get; set; }
}
