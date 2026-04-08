using CrestApps.Core.AI.Mcp.Models;
using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes;
using YesSql.Indexes;

namespace CrestApps.Core.Mvc.Web.Areas.Mcp.Indexes;

public sealed class McpPromptIndex : CatalogItemIndex, INameAwareIndex
{
    public string Name { get; set; }
}

public sealed class McpPromptIndexProvider : IndexProvider<McpPrompt>
{
    public override void Describe(DescribeContext<McpPrompt> context)
    {
        context.For<McpPromptIndex>()
            .Map(prompt => new McpPromptIndex
            {
                ItemId = prompt.ItemId,
                Name = prompt.Name,
            });
    }
}
