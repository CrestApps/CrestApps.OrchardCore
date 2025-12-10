using CrestApps.OrchardCore.AI.Chat.Models;
using CrestApps.OrchardCore.Services;

namespace CrestApps.OrchardCore.AI.Chat.Services;

/// <summary>
/// Service for managing custom AI chat instances.
/// </summary>
public interface ICustomChatInstanceManager : ISourceCatalogManager<AICustomChatInstance>
{
    /// <summary>
    /// Gets all custom chat instances for the current user.
    /// </summary>
    ValueTask<IEnumerable<AICustomChatInstance>> GetForCurrentUserAsync();

    /// <summary>
    /// Finds a custom chat instance by its ID for the current user.
    /// </summary>
    ValueTask<AICustomChatInstance> FindByIdForCurrentUserAsync(string itemId);
}
