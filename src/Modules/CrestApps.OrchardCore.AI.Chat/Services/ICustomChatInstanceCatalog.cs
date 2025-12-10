using CrestApps.OrchardCore.Services;

namespace CrestApps.OrchardCore.AI.Chat.Services;

/// <summary>
/// Interface for the custom chat instance catalog with user-scoped queries.
/// </summary>
public interface ICustomChatInstanceCatalog : ISourceCatalog<Models.AICustomChatInstance>
{
    /// <summary>
    /// Gets all instances for a specific user.
    /// </summary>
    ValueTask<IReadOnlyCollection<Models.AICustomChatInstance>> GetByUserAsync(string userId);

    /// <summary>
    /// Finds an instance by ID for a specific user.
    /// </summary>
    ValueTask<Models.AICustomChatInstance> FindByIdForUserAsync(string itemId, string userId);
}
