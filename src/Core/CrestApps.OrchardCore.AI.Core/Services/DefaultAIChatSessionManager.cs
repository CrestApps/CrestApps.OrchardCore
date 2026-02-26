using System.Security.Claims;
using CrestApps.OrchardCore.AI.Core.Indexes;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;
using OrchardCore.Modules;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class DefaultAIChatSessionManager : IAIChatSessionManager
{
    private readonly IClock _clock;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClientIPAddressAccessor _clientIPAddressAccessor;
    private readonly ISession _session;
    private readonly IAIDocumentStore _documentStore;
    private readonly IAIChatSessionPromptStore _promptStore;

    public DefaultAIChatSessionManager(
        IClock clock,
        IHttpContextAccessor httpContextAccessor,
        IClientIPAddressAccessor clientIPAddressAccessor,
        ISession session,
        IAIDocumentStore documentStore,
        IAIChatSessionPromptStore promptStore)
    {
        _clock = clock;
        _httpContextAccessor = httpContextAccessor;
        _clientIPAddressAccessor = clientIPAddressAccessor;
        _session = session;
        _documentStore = documentStore;
        _promptStore = promptStore;
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
            LastActivityUtc = _clock.UtcNow,
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

        var query = _session.QueryIndex<AIChatSessionIndex>(i => i.UserId == userId && i.Title != null && i.ProfileId != null, collection: AIConstants.CollectionName);

        if (!string.IsNullOrEmpty(context.ProfileId))
        {
            query = query.Where(i => i.ProfileId == context.ProfileId);
        }

        if (!string.IsNullOrEmpty(context.Name))
        {
            query = query.Where(i => i.Title.Contains(context.Name));
        }

        var count = await query.CountAsync();

        var indexes = await query.OrderByDescending(i => i.CreatedUtc)
            .ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ListAsync();

        return new AIChatSessionResult
        {
            Count = count,
            Sessions = indexes.Select(i => new AIChatSessionEntry
            {
                SessionId = i.SessionId,
                ProfileId = i.ProfileId,
                Title = i.Title,
                UserId = i.UserId,
                ClientId = i.ClientId,
                Status = i.Status,
                CreatedUtc = i.CreatedUtc,
                LastActivityUtc = i.LastActivityUtc,
            }),
        };
    }

    public Task<AIChatSession> FindByIdAsync(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        return _session.Query<AIChatSession, AIChatSessionIndex>(i => i.SessionId == id, collection: AIConstants.CollectionName)
                .FirstOrDefaultAsync();
    }

    public async Task<AIChatSession> FindAsync(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        var user = _httpContextAccessor.HttpContext?.User;

        if (user.Identity?.IsAuthenticated == true)
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

            return await _session.Query<AIChatSession, AIChatSessionIndex>(i => i.SessionId == id && i.UserId == userId && i.ProfileId != null, collection: AIConstants.CollectionName)
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
            return await _session.Query<AIChatSession, AIChatSessionIndex>(i => i.SessionId == id && i.UserId == null && i.ClientId == clientId, collection: AIConstants.CollectionName)
                .FirstOrDefaultAsync();
        }
    }

    public Task SaveAsync(AIChatSession chatSession)
    {
        ArgumentNullException.ThrowIfNull(chatSession);

        return _session.SaveAsync(chatSession, collection: AIConstants.CollectionName);
    }

    public async Task<bool> DeleteAsync(string sessionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        var user = _httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

        var chatSession = await _session.Query<AIChatSession, AIChatSessionIndex>(
            i => i.SessionId == sessionId && i.UserId == userId && i.ProfileId != null,
            collection: AIConstants.CollectionName)
            .FirstOrDefaultAsync();

        if (chatSession == null)
        {
            return false;
        }

        await CleanupSessionDocumentsAsync(chatSession);

        // Delete all prompts associated with this session.
        await _promptStore.DeleteAllPromptsAsync(chatSession.SessionId);

        _session.Delete(chatSession, collection: AIConstants.CollectionName);

        return true;
    }

    public async Task<int> DeleteAllAsync(string profileId)
    {
        ArgumentException.ThrowIfNullOrEmpty(profileId);

        var user = _httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated is null || user.Identity.IsAuthenticated == false)
        {
            return 0;
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

        var sessions = await _session.Query<AIChatSession, AIChatSessionIndex>(
            i => i.UserId == userId && i.ProfileId == profileId,
            collection: AIConstants.CollectionName)
            .ListAsync();

        var totalDeleted = 0;

        foreach (var session in sessions)
        {
            await CleanupSessionDocumentsAsync(session);

            // Delete all prompts associated with this session.
            await _promptStore.DeleteAllPromptsAsync(session.SessionId);

            _session.Delete(session, collection: AIConstants.CollectionName);
            totalDeleted++;
        }

        return totalDeleted;
    }

    /// <summary>
    /// Removes all documents associated with the given session from the document store
    /// and schedules deferred removal of their chunks from all AI document indexes.
    /// </summary>
    private async Task CleanupSessionDocumentsAsync(AIChatSession session)
    {
        var documents = await _documentStore.GetDocumentsAsync(
            session.SessionId,
            AIConstants.DocumentReferenceTypes.ChatSession);

        if (documents.Count == 0)
        {
            return;
        }

        var chunkIds = new List<string>();

        foreach (var doc in documents)
        {
            if (doc.Chunks != null)
            {
                for (var i = 0; i < doc.Chunks.Count; i++)
                {
                    chunkIds.Add($"{doc.ItemId}_{i}");
                }
            }

            await _documentStore.DeleteAsync(doc);
        }

        if (chunkIds.Count > 0)
        {
            ShellScope.AddDeferredTask(scope => RemoveDocumentChunksAsync(scope, chunkIds));
        }
    }

    private static async Task RemoveDocumentChunksAsync(ShellScope scope, List<string> chunkIds)
    {
        var services = scope.ServiceProvider;
        var indexStore = services.GetRequiredService<IIndexProfileStore>();

        var indexProfiles = await indexStore.GetByTypeAsync(AIConstants.AIDocumentsIndexingTaskType);

        if (!indexProfiles.Any())
        {
            return;
        }

        var logger = services.GetRequiredService<ILogger<DefaultAIChatSessionManager>>();

        foreach (var indexProfile in indexProfiles)
        {
            var documentIndexManager = services.GetKeyedService<IDocumentIndexManager>(indexProfile.ProviderName);

            if (documentIndexManager == null)
            {
                continue;
            }

            try
            {
                await documentIndexManager.DeleteDocumentsAsync(indexProfile, chunkIds);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error removing session document chunks from index '{IndexName}'.", indexProfile.IndexName);
            }
        }
    }
}
