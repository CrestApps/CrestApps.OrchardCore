namespace CrestApps.OrchardCore.AI.Memory.Models;

public sealed class AIMemoryEntryIndexDocument
{
    public string MemoryId { get; set; }

    public string UserId { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }

    public string Content { get; set; }

    public DateTime UpdatedUtc { get; set; }

    public float[] Embedding { get; set; }
}
