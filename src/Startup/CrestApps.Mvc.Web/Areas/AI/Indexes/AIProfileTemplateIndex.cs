using CrestApps.AI.Models;
using CrestApps.Data.YesSql;
using CrestApps.Data.YesSql.Indexes;
using YesSql.Indexes;

namespace CrestApps.Mvc.Web.Areas.AI.Indexes;

public sealed class AIProfileTemplateIndex : CatalogItemIndex, INameAwareIndex, ISourceAwareIndex
{
    public string Name { get; set; }

    public string Source { get; set; }
}

public sealed class AIProfileTemplateIndexProvider : IndexProvider<AIProfileTemplate>
{
    public override void Describe(DescribeContext<AIProfileTemplate> context)
    {
        context.For<AIProfileTemplateIndex>()
            .Map(template => new AIProfileTemplateIndex
            {
                ItemId = template.ItemId,
                Name = template.Name,
                Source = template.Source,
            });
    }
}
