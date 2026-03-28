using CrestApps.AI.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Indexes;
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
                var profileMetadata = template.As<ProfileTemplateMetadata>();

                return new AIProfileTemplateIndex
                {
                    ItemId = template.ItemId,
                    Source = template.Source,
                    Name = template.Name,
                    Category = template.Category,
                    ProfileType = profileMetadata.ProfileType?.ToString(),
                    IsListable = template.IsListable,
                };
            });
    }
}
