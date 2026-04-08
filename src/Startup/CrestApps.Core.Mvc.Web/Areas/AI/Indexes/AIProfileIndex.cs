using CrestApps.Core.AI.Models;
using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes;
using YesSql.Indexes;

namespace CrestApps.Core.Mvc.Web.Areas.AI.Indexes;

public sealed class AIProfileIndex : CatalogItemIndex, INameAwareIndex, ISourceAwareIndex
{
    public string Name { get; set; }

    public string Source { get; set; }
}

public sealed class AIProfileIndexProvider : IndexProvider<AIProfile>
{
    public override void Describe(DescribeContext<AIProfile> context)
    {
        context.For<AIProfileIndex>()
            .Map(profile => new AIProfileIndex
            {
                ItemId = profile.ItemId,
                Name = profile.Name,
                Source = profile.Source,
            });
    }
}
