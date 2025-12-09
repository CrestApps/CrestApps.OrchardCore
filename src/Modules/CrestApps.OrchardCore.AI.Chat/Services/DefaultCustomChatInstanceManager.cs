using System.Security.Claims;
using CrestApps.OrchardCore.AI.Chat.Indexes;
using CrestApps.OrchardCore.AI.Chat.Models;
using Microsoft.AspNetCore.Http;
using OrchardCore;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.AI.Chat.Services;

public sealed class DefaultCustomChatInstanceManager : ICustomChatInstanceManager
{
    private readonly IClock _clock;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly YesSql.ISession _session;

    public DefaultCustomChatInstanceManager(
        IClock clock,
        IHttpContextAccessor httpContextAccessor,
        YesSql.ISession session)
    {
        _clock = clock;
        _httpContextAccessor = httpContextAccessor;
        _session = session;
    }

    public Task<AICustomChatInstance> NewAsync()
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            throw new InvalidOperationException("User must be authenticated to create a custom chat instance.");
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

        var instance = new AICustomChatInstance
        {
            InstanceId = IdGenerator.GenerateId(),
            UserId = userId,
            CreatedUtc = _clock.UtcNow,
            PastMessagesCount = 10,
        };

        return Task.FromResult(instance);
    }

    public async Task<AICustomChatInstance> FindByIdAsync(string instanceId)
    {
        ArgumentException.ThrowIfNullOrEmpty(instanceId);

        var user = _httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

        return await _session.Query<AICustomChatInstance, AICustomChatInstanceIndex>(
            i => i.InstanceId == instanceId && i.UserId == userId,
            collection: AICustomChatConstants.CollectionName)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<AICustomChatInstance>> GetAllAsync()
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            return [];
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

        return await _session.Query<AICustomChatInstance, AICustomChatInstanceIndex>(
            i => i.UserId == userId,
            collection: AICustomChatConstants.CollectionName)
            .OrderByDescending(i => i.CreatedUtc)
            .ListAsync();
    }

    public Task SaveAsync(AICustomChatInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        return _session.SaveAsync(instance, collection: AICustomChatConstants.CollectionName);
    }

    public async Task<bool> DeleteAsync(string instanceId)
    {
        ArgumentException.ThrowIfNullOrEmpty(instanceId);

        var instance = await FindByIdAsync(instanceId);

        if (instance == null)
        {
            return false;
        }

        _session.Delete(instance, collection: AICustomChatConstants.CollectionName);

        return true;
    }
}
