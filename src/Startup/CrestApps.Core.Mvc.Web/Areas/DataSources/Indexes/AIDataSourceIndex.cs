using CrestApps.Core.AI.Models;
using CrestApps.Core.Data.YesSql.Indexes;
using YesSql.Indexes;

namespace CrestApps.Core.Mvc.Web.Areas.DataSources.Indexes;

public sealed class AIDataSourceIndex : CatalogItemIndex
{
    public string DisplayText { get; set; }

    public string SourceIndexProfileName { get; set; }
}

public sealed class AIDataSourceIndexProvider : IndexProvider<AIDataSource>
{
    public override void Describe(DescribeContext<AIDataSource> context)
    {
        context.For<AIDataSourceIndex>()
            .Map(ds => new AIDataSourceIndex
            {
                ItemId = ds.ItemId,
                DisplayText = ds.DisplayText,
                SourceIndexProfileName = ds.SourceIndexProfileName,
            });
    }
}
