using CrestApps.Core.AI.Mcp.Models;
using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes;
using YesSql.Indexes;

namespace CrestApps.Core.Mvc.Web.Areas.Mcp.Indexes;

public sealed class McpResourceIndex : CatalogItemIndex, ISourceAwareIndex
{
    public string DisplayText { get; set; }

    public string Source { get; set; }
}

public sealed class McpResourceIndexProvider : IndexProvider<McpResource>
{
    public override void Describe(DescribeContext<McpResource> context)
    {
        context.For<McpResourceIndex>()
            .Map(resource => new McpResourceIndex
            {
                ItemId = resource.ItemId,
                DisplayText = resource.DisplayText,
                Source = resource.Source,
            });
    }
}
