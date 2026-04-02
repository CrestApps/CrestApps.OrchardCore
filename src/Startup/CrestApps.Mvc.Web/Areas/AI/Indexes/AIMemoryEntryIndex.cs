using CrestApps.AI.Models;
using CrestApps.Data.YesSql.Indexes;
using YesSql.Indexes;

namespace CrestApps.Mvc.Web.Areas.AI.Indexes;

public sealed class AIMemoryEntryIndex : CatalogItemIndex
{
    public string UserId { get; set; }

    public string Name { get; set; }
}

public sealed class AIMemoryEntryIndexProvider : IndexProvider<AIMemoryEntry>
{
    public override void Describe(DescribeContext<AIMemoryEntry> context)
    {
        context.For<AIMemoryEntryIndex>()
            .Map(entry => new AIMemoryEntryIndex
            {
                ItemId = entry.ItemId,
                UserId = entry.UserId,
                Name = entry.Name,
            });
    }
}
