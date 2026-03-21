using CrestApps.OrchardCore.YesSql.Core.Indexes;

namespace CrestApps.OrchardCore.AI.Memory.Indexes;

public sealed class AIMemoryEntryIndex : CatalogItemIndex
{
    public string UserId { get; set; }

    public string Name { get; set; }

    public string NormalizedName { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime UpdatedUtc { get; set; }
}
