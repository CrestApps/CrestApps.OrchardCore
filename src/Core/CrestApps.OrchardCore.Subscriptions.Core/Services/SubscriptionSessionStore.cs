using System.Security.Claims;
using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

public sealed class SubscriptionSessionStore : ISubscriptionSessionStore
{
    private readonly Microsoft.AspNetCore.Http.IHttpContextAccessor _contextAccessor;
    private readonly IClientIPAddressAccessor _clientIPAddressAccessor;
    private readonly IEnumerable<ISubscriptionHandler> _subscriptionHandlers;
    private readonly ILogger<SubscriptionSessionStore> _logger;
    private readonly IClock _clock;
    private readonly ISession _session;

    public SubscriptionSessionStore(
        Microsoft.AspNetCore.Http.IHttpContextAccessor contextAccessor,
        IClientIPAddressAccessor clientIPAddressAccessor,
        IEnumerable<ISubscriptionHandler> subscriptionHandlers,
        ILogger<SubscriptionSessionStore> logger,
        IClock clock,
        ISession session)
    {
        _contextAccessor = contextAccessor;
        _clientIPAddressAccessor = clientIPAddressAccessor;
        _subscriptionHandlers = subscriptionHandlers;
        _logger = logger;
        _clock = clock;
        _session = session;
    }

    public Task<SubscriptionSession> GetAsync(string sessionId)
        => _session.Query<SubscriptionSession, SubscriptionSessionIndex>(x => x.SessionId == sessionId)
        .FirstOrDefaultAsync();

    public async Task<SubscriptionSession> GetAsync(string sessionId, SubscriptionSessionStatus status)
    {
        var query = _session.Query<SubscriptionSession, SubscriptionSessionIndex>(x => x.SessionId == sessionId && x.Status == status);

        if (_contextAccessor.HttpContext.User.Identity.IsAuthenticated)
        {
            var ownerId = CurrentUserId();

            return await query.Where(x => x.OwnerId == ownerId).FirstOrDefaultAsync();
        }

        var subscriptionSession = await query.Where(x => x.OwnerId == null).FirstOrDefaultAsync();

        // Don't trust the user, check for additional info.
        var ipAddress = (await _clientIPAddressAccessor.GetIPAddressAsync()).ToString();

        if (string.IsNullOrWhiteSpace(subscriptionSession?.IPAddress) ||
            subscriptionSession.IPAddress != ipAddress ||
            string.IsNullOrWhiteSpace(subscriptionSession?.AgentInfo) ||
            subscriptionSession.AgentInfo != _contextAccessor.HttpContext.Request.Headers.UserAgent)
        {
            // IMPORTANT: The saved session may belong to another user. Do not it.
            return null;
        }

        return subscriptionSession;
    }

    public async Task<SubscriptionSession> NewAsync(ContentItem subscriptionContentItem)
    {
        ArgumentNullException.ThrowIfNull(subscriptionContentItem);

        var subscriptionSession = await GetNewSessionAsync(subscriptionContentItem);

        var activatingContext = new SubscriptionFlowActivatingContext(subscriptionSession, subscriptionContentItem);

        await _subscriptionHandlers.InvokeAsync((handler, context) => handler.ActivatingAsync(context), activatingContext, _logger);

        var flow = new SubscriptionFlow(subscriptionSession, subscriptionContentItem);

        var activatedContext = new SubscriptionFlowActivatedContext(flow);

        subscriptionSession.CurrentStep = flow.GetFirstStep()?.Key;

        await _subscriptionHandlers.InvokeAsync((handler, context) => handler.ActivatedAsync(context), activatedContext, _logger);

        return subscriptionSession;
    }

    public Task SaveAsync(SubscriptionSession session)
        => _session.SaveAsync(session);

    private async Task<SubscriptionSession> GetNewSessionAsync(ContentItem subscriptionContentItem)
    {
        var now = _clock.UtcNow;

        var subscriptionSession = new SubscriptionSession()
        {
            SessionId = IdGenerator.GenerateId(),
            ContentType = subscriptionContentItem.ContentType,
            ContentItemId = subscriptionContentItem.ContentItemId,
            ContentItemVersionId = subscriptionContentItem.ContentItemVersionId,
            CreatedUtc = now,
            ModifiedUtc = now,
            Status = SubscriptionSessionStatus.Pending,
        };

        if (_contextAccessor.HttpContext.User.Identity.IsAuthenticated)
        {
            subscriptionSession.OwnerId = CurrentUserId();
        }
        else
        {
            subscriptionSession.IPAddress = (await _clientIPAddressAccessor.GetIPAddressAsync()).ToString();
            subscriptionSession.AgentInfo = _contextAccessor.HttpContext.Request.Headers.UserAgent;
        }

        return subscriptionSession;
    }

    private string CurrentUserId()
        => _contextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
}
