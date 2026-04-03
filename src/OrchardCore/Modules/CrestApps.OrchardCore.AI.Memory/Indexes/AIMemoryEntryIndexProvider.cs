using CrestApps.AI.Models;
using Microsoft.AspNetCore.Identity;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.AI.Memory.Indexes;

internal sealed class AIMemoryEntryIndexProvider : IndexProvider<AIMemoryEntry>
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
