using CrestApps.Core.AI.Memory;
using CrestApps.Core.AI.Models;
using Microsoft.AspNetCore.Identity;
using YesSql.Indexes;

namespace CrestApps.Core.Data.YesSql.Indexes.AIMemory;

public sealed class AIMemoryEntryIndex : CatalogItemIndex
{
    public string UserId { get; set; }

    public string Name { get; set; }

    public string NormalizedName { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime UpdatedUtc { get; set; }
}

public sealed class AIMemoryEntryIndexProvider : IndexProvider<AIMemoryEntry>
{
    private readonly ILookupNormalizer _lookupNormalizer;

    public AIMemoryEntryIndexProvider(ILookupNormalizer lookupNormalizer)
    {
        _lookupNormalizer = lookupNormalizer;
        CollectionName = MemoryConstants.CollectionName;
    }

    public override void Describe(DescribeContext<AIMemoryEntry> context)
    {
        context
            .For<AIMemoryEntryIndex>()
            .Map(memory => new AIMemoryEntryIndex
            {
                ItemId = memory.ItemId,
                UserId = memory.UserId,
                Name = memory.Name,
                NormalizedName = _lookupNormalizer.NormalizeName(memory.Name),
                CreatedUtc = memory.CreatedUtc,
                UpdatedUtc = memory.UpdatedUtc,
            });
    }
}
