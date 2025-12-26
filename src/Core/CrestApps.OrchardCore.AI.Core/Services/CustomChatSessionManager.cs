using System.Security.Claims;
using CrestApps.OrchardCore.AI.Core.Indexes;
using CrestApps.OrchardCore.AI.Models;
using YesSql;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class CustomChatSessionManager : IAICustomChatSessionManager
{
    private readonly Microsoft.AspNetCore.Http.IHttpContextAccessor _httpContextAccessor;
    private readonly ISession _session;

    public CustomChatSessionManager(
        Microsoft.AspNetCore.Http.IHttpContextAccessor httpContextAccessor,
        ISession session)
    {
        _httpContextAccessor = httpContextAccessor;
        _session = session;
    }

    public async Task<CustomChatSession> FindCustomChatSessionAsync(string sessionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        var user = _httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

        return await _session.Query<CustomChatSession, CustomChatSessionIndex>(i => i.SessionId == sessionId && i.UserId == userId).FirstOrDefaultAsync();
    }

    public async Task<CustomChatSession> FindByCustomChatInstanceIdAsync(string customChatInstanceId)
    {
        ArgumentException.ThrowIfNullOrEmpty(customChatInstanceId);

        var user = _httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

        return await _session.Query<CustomChatSession, CustomChatSessionIndex>(
            i => i.CustomChatInstanceId == customChatInstanceId && i.UserId == userId)
            .FirstOrDefaultAsync();
    }


    public Task SaveCustomChatAsync(CustomChatSession customChatSession)
    {
        ArgumentNullException.ThrowIfNull(customChatSession);

        return _session.SaveAsync(customChatSession);
    }

    public async Task<bool> DeleteCustomChatAsync(string sessionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        var user = _httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

        var customChatSession = await _session.Query<CustomChatSession, CustomChatSessionIndex>(
            i => i.SessionId == sessionId && i.UserId == userId).FirstOrDefaultAsync();

        if (customChatSession == null)
        {
            return false;
        }

        _session.Delete(customChatSession);

        return true;
    }
}

