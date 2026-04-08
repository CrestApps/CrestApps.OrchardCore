using CrestApps.Core.Models;

namespace CrestApps.Core.AI.Models;

public sealed class AIProfileQueryContext : QueryContext
{
    public bool IsListableOnly { get; set; }
}
