using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Subscriptions.Core.Extensions;

public static class ContentDefinitionManagerExtensions
{
    public static async Task<IEnumerable<ContentTypeDefinition>> GetSubscriptionsTypeDefinitionsAsync(this IContentDefinitionManager manager)
    {
        var types = await manager.ListTypeDefinitionsAsync();

        return types.Where(x => x.StereotypeEquals(SubscriptionConstants.Stereotype));
    }
}
