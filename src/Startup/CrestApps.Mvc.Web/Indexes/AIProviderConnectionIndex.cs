using CrestApps.AI.Models;
using CrestApps.Data.YesSql;
using CrestApps.Data.YesSql.Indexes;
using YesSql.Indexes;

namespace CrestApps.Mvc.Web.Indexes;

public sealed class AIProviderConnectionIndex : CatalogItemIndex, INameAwareIndex, ISourceAwareIndex
{
    public string DisplayText { get; set; }
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
                DisplayText = connection.Name,
                Source = connection.Source,
            });
    }
}
