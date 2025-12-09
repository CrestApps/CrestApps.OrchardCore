using CrestApps.OrchardCore.AI.Chat.Models;

namespace CrestApps.OrchardCore.AI.Chat.Services;

/// <summary>
/// Service for managing custom AI chat instances.
/// </summary>
public interface ICustomChatInstanceManager
{
    /// <summary>
    /// Creates a new custom chat instance.
    /// </summary>
    Task<AICustomChatInstance> NewAsync();

    /// <summary>
    /// Finds a custom chat instance by its ID.
    /// </summary>
    Task<AICustomChatInstance> FindByIdAsync(string instanceId);

    /// <summary>
    /// Gets all custom chat instances for the current user.
    /// </summary>
    Task<IEnumerable<AICustomChatInstance>> GetAllAsync();

    /// <summary>
    /// Saves a custom chat instance.
    /// </summary>
    Task SaveAsync(AICustomChatInstance instance);

    /// <summary>
    /// Deletes a custom chat instance.
    /// </summary>
    Task<bool> DeleteAsync(string instanceId);
}
