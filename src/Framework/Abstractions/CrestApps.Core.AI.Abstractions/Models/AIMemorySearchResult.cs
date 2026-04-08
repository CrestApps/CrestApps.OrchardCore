namespace CrestApps.Core.AI.Models;

public sealed class AIMemorySearchResult
{
    public string MemoryId { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }

    public string Content { get; set; }

    public DateTime? UpdatedUtc { get; set; }

    public float Score { get; set; }
}
