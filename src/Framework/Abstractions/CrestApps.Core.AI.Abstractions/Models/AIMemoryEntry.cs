using CrestApps.Core.Models;

namespace CrestApps.Core.AI.Models;

public sealed class AIMemoryEntry : CatalogItem
{
    public string UserId { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }

    public string Content { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime UpdatedUtc { get; set; }
}
