using CrestApps.OrchardCore.AgenticCommerce.Core;
using CrestApps.OrchardCore.AgenticCommerce.Core.Indexes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.AgenticCommerce.Indexes;

internal sealed class AgentPlatformIndexProvider : IndexProvider<AgentPlatform>
{
    public AgentPlatformIndexProvider()
    {
        CollectionName = AgenticCommerceStorage.CollectionName;
    }

    public override void Describe(DescribeContext<AgentPlatform> context)
    {
        context
            .For<AgentPlatformIndex>()
            .Map(platform => new AgentPlatformIndex
            {
                ItemId = platform.ItemId,
                Name = platform.Name,
                Enabled = platform.Enabled,
                ApiKeyHash = platform.ApiKeyHash,
                OAuthClientId = platform.OAuthClientId,
                ProfileUrl = platform.ProfileUrl,
            });
    }
}
