using CrestApps.Core.AI.Models;
using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes;
using YesSql.Indexes;

namespace CrestApps.Core.Mvc.Web.Areas.AI.Indexes;

public sealed class AIProviderConnectionIndex : CatalogItemIndex, INameAwareIndex, ISourceAwareIndex
{
    public string Name { get; set; }

    public string Source { get; set; }
}

public sealed class AIProviderConnectionIndexProvider : IndexProvider<AIProviderConnection>
{
    public override void Describe(DescribeContext<AIProviderConnection> context)
    {
        context.For<AIProviderConnectionIndex>()
            .Map(connection => new AIProviderConnectionIndex
            {
                ItemId = connection.ItemId,
                Name = connection.Name,
                Source = connection.Source,
            });
    }
}
