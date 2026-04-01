using CrestApps.AI.Mcp.Models;
using CrestApps.Data.YesSql;
using CrestApps.Data.YesSql.Indexes;
using YesSql.Indexes;

namespace CrestApps.Mvc.Web.Indexes;

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
