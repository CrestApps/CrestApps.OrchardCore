using CrestApps.Core.AI.A2A.Models;
using CrestApps.Core.Data.YesSql.Indexes;
using YesSql.Indexes;

namespace CrestApps.Core.Mvc.Web.Areas.A2A.Indexes;

public sealed class A2AConnectionIndex : CatalogItemIndex
{
    public string DisplayText { get; set; }
}

public sealed class A2AConnectionIndexProvider : IndexProvider<A2AConnection>
{
    public override void Describe(DescribeContext<A2AConnection> context)
    {
        context.For<A2AConnectionIndex>()
            .Map(connection => new A2AConnectionIndex
            {
                ItemId = connection.ItemId,
                DisplayText = connection.DisplayText,
            });
    }
}
