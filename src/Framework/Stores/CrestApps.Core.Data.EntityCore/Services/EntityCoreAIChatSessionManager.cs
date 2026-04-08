using CrestApps.Core.AI;
using System.Security.Claims;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.ResponseHandling;
using CrestApps.Core.Data.EntityCore.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

namespace CrestApps.Core.Data.EntityCore.Services;

public sealed class EntityCoreAIChatSessionManager : IAIChatSessionManager
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly CrestAppsEntityDbContext _dbContext;
    private readonly IAIChatSessionPromptStore _promptStore;
    private readonly TimeProvider _timeProvider;

    public EntityCoreAIChatSessionManager(
        IHttpContextAccessor httpContextAccessor,
        CrestAppsEntityDbContext dbContext,
        IAIChatSessionPromptStore promptStore,
        TimeProvider timeProvider)
    {
        _httpContextAccessor = httpContextAccessor;
        _dbContext = dbContext;
        _promptStore = promptStore;
        _timeProvider = timeProvider;
    }

    public async Task<AIChatSession> FindByIdAsync(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        var record = await _dbContext.AIChatSessionRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.SessionId == id);

        return record is null ? null : Materialize(record);
    }

    public Task<AIChatSession> FindAsync(string id)
        => FindByIdAsync(id);

    public async Task<AIChatSessionResult> PageAsync(int page, int pageSize, AIChatSessionQueryContext context = null)
    {
        var query = _dbContext.AIChatSessionRecords.AsNoTracking();

        if (!string.IsNullOrEmpty(context?.ProfileId))
        {
            query = query.Where(x => x.ProfileId == context.ProfileId);
        }

        var skip = (page - 1) * pageSize;
        var total = await query.CountAsync();
        var records = await query
            .OrderByDescending(x => x.CreatedUtc)
            .ThenByDescending(x => x.LastActivityUtc)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();

        return new AIChatSessionResult
        {
            Count = total,
            Sessions = records.Select(s => new AIChatSessionEntry
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

        var record = await _dbContext.AIChatSessionRecords
            .FirstOrDefaultAsync(x => x.SessionId == chatSession.SessionId);

        if (record is null)
        {
            _dbContext.AIChatSessionRecords.Add(CreateRecord(chatSession));
        }
        else
        {
            UpdateRecord(record, chatSession);
        }

        await _dbContext.SaveChangesAsync();
    }

    public async Task<bool> DeleteAsync(string sessionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        var record = await _dbContext.AIChatSessionRecords
            .FirstOrDefaultAsync(x => x.SessionId == sessionId);

        if (record is null)
        {
            return false;
        }

        _dbContext.AIChatSessionRecords.Remove(record);
        await _dbContext.SaveChangesAsync();

        return true;
    }

    public async Task<int> DeleteAllAsync(string profileId)
    {
        ArgumentException.ThrowIfNullOrEmpty(profileId);

        var records = await _dbContext.AIChatSessionRecords
            .Where(x => x.ProfileId == profileId)
            .ToListAsync();

        if (records.Count == 0)
        {
            return 0;
        }

        _dbContext.AIChatSessionRecords.RemoveRange(records);
        await _dbContext.SaveChangesAsync();

        return records.Count;
    }

    private static AIChatSession Materialize(AIChatSessionRecord record)
        => EntityCoreStoreSerializer.Deserialize<AIChatSession>(record.Payload);

    private static AIChatSessionRecord CreateRecord(AIChatSession session)
        => new()
        {
            SessionId = session.SessionId,
            ProfileId = session.ProfileId,
            Title = session.Title,
            UserId = session.UserId,
            ClientId = session.ClientId,
            Status = session.Status,
            CreatedUtc = session.CreatedUtc,
            LastActivityUtc = session.LastActivityUtc,
            Payload = EntityCoreStoreSerializer.Serialize(session),
        };

    private static void UpdateRecord(AIChatSessionRecord record, AIChatSession session)
    {
        record.ProfileId = session.ProfileId;
        record.Title = session.Title;
        record.UserId = session.UserId;
        record.ClientId = session.ClientId;
        record.Status = session.Status;
        record.CreatedUtc = session.CreatedUtc;
        record.LastActivityUtc = session.LastActivityUtc;
        record.Payload = EntityCoreStoreSerializer.Serialize(session);
    }
}
