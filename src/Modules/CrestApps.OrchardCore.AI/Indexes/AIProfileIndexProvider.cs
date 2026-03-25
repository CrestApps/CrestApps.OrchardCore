using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Indexes;
using CrestApps.OrchardCore.AI.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.AI.Indexes;

internal sealed class AIProfileIndexProvider : IndexProvider<AIProfile>
{
    public AIProfileIndexProvider()
    {
        CollectionName = AIConstants.AICollectionName;
    }

    public override void Describe(DescribeContext<AIProfile> context)
    {
        context
            .For<AIProfileIndex>()
            .Map(profile =>
            {
                var settings = profile.GetSettings<AIProfileSettings>();

                return new AIProfileIndex
                {
                    ItemId = profile.ItemId,
                    Name = profile.Name,
                    Type = profile.Type.ToString(),
                    Description = profile.Description,
                    DeploymentId = profile.ChatDeploymentId,
                    OrchestratorName = profile.OrchestratorName,
                    OwnerId = profile.OwnerId,
                    Author = profile.Author,
                    IsListable = settings.IsListable,
                };
            });
    }
}
