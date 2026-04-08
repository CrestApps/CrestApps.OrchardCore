using System.Security.Claims;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.ResponseHandling;
using CrestApps.Core.Mvc.Web.Areas.AIChat.Indexes;
using Microsoft.Extensions.AI;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.Core.Mvc.Web.Areas.AIChat.Services;

public sealed class YesSqlAIChatSessionManager : IAIChatSessionManager
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ISession _session;
    private readonly IAIChatSessionPromptStore _promptStore;
    private readonly TimeProvider _timeProvider;

    public YesSqlAIChatSessionManager(
        IHttpContextAccessor httpContextAccessor,
        ISession session,
        IAIChatSessionPromptStore promptStore,
        TimeProvider timeProvider)
    {
        _httpContextAccessor = httpContextAccessor;
        _session = session;
        _promptStore = promptStore;
        _timeProvider = timeProvider;
    }

    public async Task<AIChatSession> FindByIdAsync(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        return await _session.Query<AIChatSession, AIChatSessionIndex>(x => x.SessionId == id).FirstOrDefaultAsync();
    }

    public async Task<AIChatSession> FindAsync(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        return await _session.Query<AIChatSession, AIChatSessionIndex>(x => x.SessionId == id).FirstOrDefaultAsync();
    }

    public async Task<AIChatSessionResult> PageAsync(int page, int pageSize, AIChatSessionQueryContext context = null)
    {
        var query = _session.Query<AIChatSession, AIChatSessionIndex>();

        if (!string.IsNullOrEmpty(context?.ProfileId))
        {
            query = query.Where(x => x.ProfileId == context.ProfileId);
        }

        var skip = (page - 1) * pageSize;
        var total = await query.CountAsync();
        var items = (await query.ListAsync())
            .OrderByDescending(x => x.CreatedUtc)
            .ThenByDescending(x => x.LastActivityUtc)
            .Skip(skip)
            .Take(pageSize);

        return new AIChatSessionResult
        {
            Count = total,
            Sessions = items.Select(s => new AIChatSessionEntry
            {
                SessionId = s.SessionId,
                ProfileId = s.ProfileId,
                Title = s.Title,
                UserId = s.UserId,
                ClientId = s.ClientId,
                Status = s.Status,
                CreatedUtc = s.CreatedUtc,
                LastActivityUtc = s.LastActivityUtc,
            }),
        };
    }

    public async Task<AIChatSession> NewAsync(AIProfile profile, NewAIChatSessionContext context)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var session = new AIChatSession
        {
            SessionId = UniqueId.GenerateId(),
            ProfileId = profile.ItemId,
            CreatedUtc = now,
            LastActivityUtc = now,
            Status = ChatSessionStatus.Active,
        };

        var user = _httpContextAccessor.HttpContext?.User;
        var userId = user?.FindFirstValue(ClaimTypes.NameIdentifier) ?? user?.Identity?.Name;

        if (!string.IsNullOrEmpty(userId))
        {
            session.UserId = userId;
        }

        if (profile.Type == AIProfileType.Chat)
        {
            var profileMetadata = profile.As<AIProfileMetadata>();

            if (!string.IsNullOrWhiteSpace(profileMetadata.InitialPrompt))
            {
                await _promptStore.CreateAsync(new AIChatSessionPrompt
                {
                    ItemId = UniqueId.GenerateId(),
                    SessionId = session.SessionId,
                    Role = ChatRole.Assistant,
                    Title = profile.PromptSubject,
                    Content = profileMetadata.InitialPrompt,
                    CreatedUtc = now,
                });
            }

            var handlerSettings = profile.GetSettings<ResponseHandlerProfileSettings>();

            if (!string.IsNullOrEmpty(handlerSettings.InitialResponseHandlerName))
            {
                session.ResponseHandlerName = handlerSettings.InitialResponseHandlerName;
            }
        }

        return session;
    }

    public async Task SaveAsync(AIChatSession chatSession)
    {
        ArgumentNullException.ThrowIfNull(chatSession);

        chatSession.LastActivityUtc = _timeProvider.GetUtcNow().UtcDateTime;
        await _session.SaveAsync(chatSession);
        await _session.SaveChangesAsync();
    }

    public async Task<bool> DeleteAsync(string sessionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        var session = await _session.Query<AIChatSession, AIChatSessionIndex>(x => x.SessionId == sessionId).FirstOrDefaultAsync();

        if (session == null)
        {
            return false;
        }

        _session.Delete(session);
        await _session.SaveChangesAsync();

        return true;
    }

    public async Task<int> DeleteAllAsync(string profileId)
    {
        var sessions = await _session.Query<AIChatSession, AIChatSessionIndex>(x => x.ProfileId == profileId).ListAsync();
        var count = 0;

        foreach (var s in sessions)
        {
            _session.Delete(s);
            count++;
        }

        await _session.SaveChangesAsync();

        return count;
    }
}
