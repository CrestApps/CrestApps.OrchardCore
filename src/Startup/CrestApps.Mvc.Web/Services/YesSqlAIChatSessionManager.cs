using CrestApps.AI;
using CrestApps.AI.Models;
using CrestApps.Mvc.Web.Indexes;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.Mvc.Web.Services;

public sealed class YesSqlAIChatSessionManager : IAIChatSessionManager
{
    private readonly ISession _session;

    public YesSqlAIChatSessionManager(ISession session)
    {
        _session = session;
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
        var items = await query.Skip(skip).Take(pageSize).ListAsync();

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

    public Task<AIChatSession> NewAsync(AIProfile profile, NewAIChatSessionContext context)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var session = new AIChatSession
        {
            SessionId = Guid.NewGuid().ToString("N"),
            ProfileId = profile.ItemId,
            CreatedUtc = DateTime.UtcNow,
            LastActivityUtc = DateTime.UtcNow,
            Status = ChatSessionStatus.Active,
        };

        return Task.FromResult(session);
    }

    public async Task SaveAsync(AIChatSession chatSession)
    {
        ArgumentNullException.ThrowIfNull(chatSession);

        chatSession.LastActivityUtc = DateTime.UtcNow;
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
