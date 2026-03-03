using CrestApps.AI.Models;
using CrestApps.Data.YesSql;
using CrestApps.Data.YesSql.Indexes;
using YesSql.Indexes;

namespace CrestApps.Mvc.Web.Indexes;

public sealed class AIProfileIndex : CatalogItemIndex, INameAwareIndex, ISourceAwareIndex
{
    public string DisplayText { get; set; }
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
                DisplayText = profile.Name,
                Source = profile.Source,
            });
    }
}
