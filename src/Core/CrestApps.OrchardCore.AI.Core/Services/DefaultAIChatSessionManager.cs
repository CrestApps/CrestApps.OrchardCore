using System.Security.Claims;
using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.ResponseHandling;
using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.AIChat;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Modules;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Default implementation of <see cref="IAIChatSessionManager"/> that manages
/// chat sessions using YesSql storage and the current HTTP user context.
/// </summary>
public sealed class DefaultAIChatSessionManager : IAIChatSessionManager
{
    private readonly IClock _clock;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClientIPAddressAccessor _clientIPAddressAccessor;
    private readonly ISession _session;
    private readonly IAIChatSessionPromptStore _promptStore;

    private readonly IEnumerable<IAIChatSessionHandler> _handlers;
    private readonly YesSqlStoreOptions _yesSqlStoreOptions;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultAIChatSessionManager"/> class.
    /// </summary>
    /// <param name="clock">The clock for UTC timestamps.</param>
    /// <param name="httpContextAccessor">The accessor for the current HTTP context.</param>
    /// <param name="clientIPAddressAccessor">The accessor for deriving client identifiers.</param>
    /// <param name="session">The YesSql session used for persistence.</param>
    /// <param name="promptStore">The store for chat session prompts.</param>
    /// <param name="handlers">The chat session lifecycle handlers.</param>
    /// <param name="logger">The logger instance.</param>
    public DefaultAIChatSessionManager(
        IClock clock,
        IHttpContextAccessor httpContextAccessor,
        IClientIPAddressAccessor clientIPAddressAccessor,
        ISession session,
        IAIChatSessionPromptStore promptStore,
        IEnumerable<IAIChatSessionHandler> handlers,
        IOptions<YesSqlStoreOptions> yesSqlStoreOptions,
        ILogger<DefaultAIChatSessionManager> logger)
    {
        _clock = clock;
        _httpContextAccessor = httpContextAccessor;
        _clientIPAddressAccessor = clientIPAddressAccessor;
        _session = session;
        _promptStore = promptStore;
        _handlers = handlers;
        _yesSqlStoreOptions = yesSqlStoreOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new chat session for the specified AI profile. Associates the session
    /// with the current authenticated user or, for anonymous users, a hashed client identifier.
    /// </summary>
    /// <param name="profile">The AI profile to create a session for.</param>
    /// <param name="context">The context containing options for the new session.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The newly created <see cref="AIChatSession"/>.</returns>
    public async Task<AIChatSession> NewAsync(
        AIProfile profile,
        NewAIChatSessionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(context);

        var chatSession = new AIChatSession
        {
            SessionId = UniqueId.GenerateId(),
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

        if (profile.Type == AIProfileType.Chat)
        {
            var profileMetadata = profile.GetOrCreate<AIProfileMetadata>();
            var initialPrompt = profileMetadata.InitialPrompt;

            if (!string.IsNullOrEmpty(initialPrompt))
            {
                await _promptStore.CreateAsync(new AIChatSessionPrompt
                {
                    ItemId = IdGenerator.GenerateId(),
                    SessionId = chatSession.SessionId,
                    Role = ChatRole.Assistant,
                    Title = profile.PromptSubject,
                    Content = initialPrompt,
                    CreatedUtc = _clock.UtcNow,
                }, cancellationToken);
            }

            // Set the initial response handler from profile settings.
            var handlerSettings = profile.GetSettings<ResponseHandlerProfileSettings>();

            if (!string.IsNullOrEmpty(handlerSettings.InitialResponseHandlerName))
            {
                chatSession.ResponseHandlerName = handlerSettings.InitialResponseHandlerName;
            }
        }

        return chatSession;
    }

    /// <summary>
    /// Returns a paginated list of chat sessions belonging to the current authenticated user.
    /// </summary>
    /// <param name="page">The one-based page number.</param>
    /// <param name="pageSize">The number of sessions per page.</param>
    /// <param name="context">The query context containing optional filters such as profile ID and name.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An <see cref="AIChatSessionResult"/> containing the total count and the requested page of sessions.</returns>
    public async Task<AIChatSessionResult> PageAsync(
        int page,
        int pageSize,
        AIChatSessionQueryContext context,
        CancellationToken cancellationToken = default)
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

        var query = _session.Query<AIChatSession, AIChatSessionIndex>(i => i.UserId == userId, collection: _yesSqlStoreOptions.AICollectionName);

        if (!string.IsNullOrEmpty(context.ProfileId))
        {
            query = query.Where(i => i.ProfileId == context.ProfileId);
        }

        var sessions = (await query.ListAsync(cancellationToken))
            .Where(session => session.ProfileId is not null);

        if (!string.IsNullOrEmpty(context.Name))
        {
            sessions = sessions.Where(session => session.Title?.Contains(context.Name, StringComparison.OrdinalIgnoreCase) == true);
        }

        var filteredSessions = sessions
            .OrderByDescending(session => session.CreatedUtc)
            .ThenByDescending(session => session.LastActivityUtc)
            .ToArray();

        var count = filteredSessions.Length;
        var pageSessions = filteredSessions
            .Skip((page - 1) * pageSize)
            .Take(pageSize);

        return new AIChatSessionResult
        {
            Count = count,
            Sessions = pageSessions.Select(session => new AIChatSessionEntry
            {
                SessionId = session.SessionId,
                ProfileId = session.ProfileId,
                Title = session.Title,
                UserId = session.UserId,
                ClientId = session.ClientId,
                Status = session.Status,
                CreatedUtc = session.CreatedUtc,
                LastActivityUtc = session.LastActivityUtc,
            }),
        };
    }

    /// <summary>
    /// Finds a chat session by its unique session identifier without user-scoping.
    /// </summary>
    /// <param name="id">The unique session identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The matching <see cref="AIChatSession"/>, or <see langword="null"/> if not found.</returns>
    public Task<AIChatSession> FindByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        return _session.Query<AIChatSession, AIChatSessionIndex>(i => i.SessionId == id, collection: _yesSqlStoreOptions.AICollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Finds a chat session by its identifier, scoped to the current user. Authenticated users
    /// match by user ID; anonymous users match by a hashed client identifier.
    /// </summary>
    /// <param name="id">The unique session identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The matching <see cref="AIChatSession"/>, or <see langword="null"/> if not found or not owned by the current user.</returns>
    public async Task<AIChatSession> FindAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        var user = _httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated == true)
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

            return await _session.Query<AIChatSession, AIChatSessionIndex>(i => i.SessionId == id && i.UserId == userId && i.ProfileId != null, collection: _yesSqlStoreOptions.AICollectionName)
                .FirstOrDefaultAsync(cancellationToken);
        }
        else
        {
            var clientId = await _clientIPAddressAccessor.GetClientIdAsync(_httpContextAccessor.HttpContext);

            if (string.IsNullOrEmpty(clientId))
            {
                throw new InvalidOperationException("Unable to find the clientId. Possible Robot.");
            }

            var chatSession = await _session.Query<AIChatSession, AIChatSessionIndex>(i => i.SessionId == id, collection: _yesSqlStoreOptions.AICollectionName)
                .FirstOrDefaultAsync(cancellationToken);

            if (chatSession?.UserId is not null || chatSession?.ClientId != clientId)
            {
                return null;
            }

            return chatSession;
        }
    }

    /// <summary>
    /// Persists the specified chat session to the data store.
    /// </summary>
    /// <param name="chatSession">The chat session to save.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public Task SaveAsync(AIChatSession chatSession, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chatSession);

        return _session.SaveAsync(chatSession, collection: _yesSqlStoreOptions.AICollectionName, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Deletes a chat session and all of its prompts for the current authenticated user.
    /// </summary>
    /// <param name="sessionId">The unique session identifier to delete.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="true"/> if the session was found and deleted; otherwise <see langword="false"/>.</returns>
    public async Task<bool> DeleteAsync(string sessionId, CancellationToken cancellationToken = default)
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
            collection: _yesSqlStoreOptions.AICollectionName)
                .FirstOrDefaultAsync(cancellationToken);

        if (chatSession == null)
        {
            return false;
        }

        var deletingContext = new CrestApps.Core.Models.DeletingContext<AIChatSession>(chatSession);
        await _handlers.InvokeAsync((handler, ctx) => handler.DeletingAsync(ctx), deletingContext, _logger);

        // Delete all prompts associated with this session.

        await _promptStore.DeleteAllPromptsAsync(chatSession.SessionId);

        _session.Delete(chatSession, collection: _yesSqlStoreOptions.AICollectionName);

        var deletedContext = new CrestApps.Core.Models.DeletedContext<AIChatSession>(chatSession);
        await _handlers.InvokeAsync((handler, ctx) => handler.DeletedAsync(ctx), deletedContext, _logger);

        return true;
    }

    /// <summary>
    /// Deletes all chat sessions and their prompts for the specified profile, scoped to the current authenticated user.
    /// </summary>
    /// <param name="profileId">The AI profile identifier whose sessions should be deleted.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The number of sessions deleted.</returns>
    public async Task<int> DeleteAllAsync(string profileId, CancellationToken cancellationToken = default)
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
            collection: _yesSqlStoreOptions.AICollectionName)
                .ListAsync(cancellationToken);

        var totalDeleted = 0;

        foreach (var session in sessions)
        {
            var deletingContext = new CrestApps.Core.Models.DeletingContext<AIChatSession>(session);
            await _handlers.InvokeAsync((handler, ctx) => handler.DeletingAsync(ctx), deletingContext, _logger);

            // Delete all prompts associated with this session.

            await _promptStore.DeleteAllPromptsAsync(session.SessionId);

            _session.Delete(session, collection: _yesSqlStoreOptions.AICollectionName);

            var deletedContext = new CrestApps.Core.Models.DeletedContext<AIChatSession>(session);
            await _handlers.InvokeAsync((handler, ctx) => handler.DeletedAsync(ctx), deletedContext, _logger);

            totalDeleted++;
        }

        return totalDeleted;
    }
}
