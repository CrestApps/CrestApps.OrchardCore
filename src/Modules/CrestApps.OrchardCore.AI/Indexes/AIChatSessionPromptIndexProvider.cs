using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Indexes;
using CrestApps.OrchardCore.AI.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.AI.Indexes;

internal sealed class AIChatSessionPromptIndexProvider : IndexProvider<AIChatSessionPrompt>
{
    public AIChatSessionPromptIndexProvider()
    {
        CollectionName = AIConstants.CollectionName;
    }

    public override void Describe(DescribeContext<AIChatSessionPrompt> context)
    {
        context
            .For<AIChatSessionPromptIndex>()
            .Map(prompt => new AIChatSessionPromptIndex
            {
                ItemId = prompt.ItemId,
                SessionId = prompt.SessionId,
                Role = prompt.Role.Value,
                CreatedUtc = prompt.CreatedUtc,
            });
    }
}
