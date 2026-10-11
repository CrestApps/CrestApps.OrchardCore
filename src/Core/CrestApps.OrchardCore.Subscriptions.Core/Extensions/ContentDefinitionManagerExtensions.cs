using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Subscriptions.Core.Extensions;

/// <summary>
/// Provides helper methods for querying subscription content type definitions.
/// </summary>
public static class ContentDefinitionManagerExtensions
{
    /// <summary>
    /// Gets the content type definitions that use the subscription stereotype.
    /// </summary>
    /// <param name="manager">The content definition manager used to load content type definitions.</param>
    /// <returns>The content type definitions that are configured as subscription types.</returns>
    public static async Task<IEnumerable<ContentTypeDefinition>> GetSubscriptionsTypeDefinitionsAsync(this IContentDefinitionManager manager)
    {
        var types = await manager.ListTypeDefinitionsAsync();

        return types.Where(x => x.StereotypeEquals(SubscriptionConstants.Stereotype));
    }
}
