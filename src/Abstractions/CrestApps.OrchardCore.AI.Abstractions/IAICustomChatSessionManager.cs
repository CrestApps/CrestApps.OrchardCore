using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI;

public interface IAICustomChatSessionManager
{
    Task<CustomChatSession> FindByCustomChatInstanceIdAsync(string customChatInstanceId);

    Task<CustomChatSession> FindCustomChatSessionAsync(string sessionId);

    Task SaveCustomChatAsync(CustomChatSession customChatSession);

    Task<bool> DeleteCustomChatAsync(string sessionId);
}
