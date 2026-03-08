using CrestApps.AI.Models;
using CrestApps.Data.YesSql.Indexes;
using YesSql.Indexes;

namespace CrestApps.Mvc.Web.Indexes;

public sealed class AIChatSessionPromptIndex : CatalogItemIndex
{
    public string SessionId { get; set; }
    public string Role { get; set; }
}

public sealed class AIChatSessionPromptIndexProvider : IndexProvider<AIChatSessionPrompt>
{
    public override void Describe(DescribeContext<AIChatSessionPrompt> context)
    {
        context.For<AIChatSessionPromptIndex>()
            .Map(prompt => new AIChatSessionPromptIndex
            {
                ItemId = prompt.ItemId,
                SessionId = prompt.SessionId,
                Role = prompt.Role.Value,
            });
    }
}
