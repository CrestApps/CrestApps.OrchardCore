using System.Security.Claims;
using CrestApps.OrchardCore.AI.Core.Indexes;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Http;
using OrchardCore;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class DefaultAIChatSessionManager : IAIChatSessionManager
{
    private readonly IClock _clock;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClientIPAddressAccessor _clientIPAddressAccessor;
    private readonly YesSql.ISession _session;

    public DefaultAIChatSessionManager(
        IClock clock,
        IHttpContextAccessor httpContextAccessor,
        IClientIPAddressAccessor clientIPAddressAccessor,
        YesSql.ISession session)
    {
        _clock = clock;
        _httpContextAccessor = httpContextAccessor;
        _clientIPAddressAccessor = clientIPAddressAccessor;
        _session = session;
    }

    public async Task<AIChatSession> NewAsync(AIProfile profile, NewAIChatSessionContext context)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(context);

        var chatSession = new AIChatSession
        {
            SessionId = IdGenerator.GenerateId(),
            ProfileId = profile.ItemId,
            CreatedUtc = _clock.UtcNow,
        };

        var user = _httpContextAccessor.HttpContext?.User;

        if (user.Identity?.IsAuthenticated == true)
        {
            chatSession.UserId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        }
        else if (!context.AllowRobots)
        {
            var clientId = await _clientIPAddressAccessor.GetClientIdAsync(_httpContextAccessor.HttpContext);

            if (string.IsNullOrEmpty(clientId))
            {
                throw new InvalidOperationException("Unable to find the clientId. Possible Robot.");
            }

            chatSession.ClientId = clientId;
        }

        return chatSession;
    }

    public async Task<AIChatSessionResult> PageAsync(int page, int pageSize, AIChatSessionQueryContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var user = _httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated is null || user.Identity.IsAuthenticated == false)
        {
            return new AIChatSessionResult
            {
                Count = 0,
                Sessions = [],
            };
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

        var query = _session.Query<AIChatSession, AIChatSessionIndex>(i => i.UserId == userId && i.Title != null, collection: AIConstants.CollectionName);

        if (!string.IsNullOrEmpty(context.ProfileId))
        {
            query = query.Where(i => i.ProfileId == context.ProfileId);
        }

        if (!string.IsNullOrEmpty(context.Name))
        {
            query = query.Where(i => i.Title.Contains(context.Name));
        }

        return new AIChatSessionResult
        {
            Count = await query.CountAsync(),
            Sessions = await query.OrderByDescending(i => i.CreatedUtc)
            .ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ListAsync()
        };
    }

    public async Task<AIChatSession> FindAsync(string sessionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        var user = _httpContextAccessor.HttpContext?.User;

        if (user.Identity?.IsAuthenticated == true)
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

            return await _session.Query<AIChatSession, AIChatSessionIndex>(i => i.SessionId == sessionId && i.UserId == userId, collection: AIConstants.CollectionName)
                .FirstOrDefaultAsync();
        }
        else
        {
            var clientId = await _clientIPAddressAccessor.GetClientIdAsync(_httpContextAccessor.HttpContext);

            if (string.IsNullOrEmpty(clientId))
            {
                throw new InvalidOperationException("Unable to find the clientId. Possible Robot.");
            }

            // It's important to make sure that the userId is null when querying using clientId.
            return await _session.Query<AIChatSession, AIChatSessionIndex>(i => i.SessionId == sessionId && i.UserId == null && i.ClientId == clientId, collection: AIConstants.CollectionName)
                .FirstOrDefaultAsync();
        }
    }

    public Task SaveAsync(AIChatSession chatSession)
    {
        ArgumentNullException.ThrowIfNull(chatSession);

        return _session.SaveAsync(chatSession, collection: AIConstants.CollectionName);
    }
}
