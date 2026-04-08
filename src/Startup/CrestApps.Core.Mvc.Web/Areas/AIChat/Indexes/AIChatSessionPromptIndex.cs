using CrestApps.Core.AI.Models;
using CrestApps.Core.Data.YesSql.Indexes;
using YesSql.Indexes;

namespace CrestApps.Core.Mvc.Web.Areas.AIChat.Indexes;

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
