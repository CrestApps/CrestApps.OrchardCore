using CrestApps.Core.AI.Mcp.Models;
using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes;
using YesSql.Indexes;

namespace CrestApps.Core.Mvc.Web.Areas.Mcp.Indexes;

public sealed class McpConnectionIndex : CatalogItemIndex, ISourceAwareIndex
{
    public string DisplayText { get; set; }

    public string Source { get; set; }
}

public sealed class McpConnectionIndexProvider : IndexProvider<McpConnection>
{
    public override void Describe(DescribeContext<McpConnection> context)
    {
        context.For<McpConnectionIndex>()
            .Map(connection => new McpConnectionIndex
            {
                ItemId = connection.ItemId,
                DisplayText = connection.DisplayText,
                Source = connection.Source,
            });
    }
}
