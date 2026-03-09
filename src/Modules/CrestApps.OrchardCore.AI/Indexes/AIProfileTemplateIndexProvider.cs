using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Indexes;
using CrestApps.OrchardCore.AI.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.AI.Indexes;

internal sealed class AIProfileTemplateIndexProvider : IndexProvider<AIProfileTemplate>
{
    public AIProfileTemplateIndexProvider()
    {
        CollectionName = AIConstants.AICollectionName;
    }

    public override void Describe(DescribeContext<AIProfileTemplate> context)
    {
        context
            .For<AIProfileTemplateIndex>()
            .Map(template =>
            {
                return new AIProfileTemplateIndex
                {
                    ItemId = template.ItemId,
                    Name = template.Name,
                    Category = template.Category,
                    ProfileType = template.ProfileType?.ToString(),
                    IsListable = template.IsListable,
                };
            });
    }
}
