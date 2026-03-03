using CrestApps.Models;

namespace CrestApps.AI.Models;

public sealed class AIProfileQueryContext : QueryContext
{
    public bool IsListableOnly { get; set; }
}
