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

    public async Task<SubscriptionSession> GetAsync(string sessionId, SubscriptionSessionStatus status)
    {
        SubscriptionSession subscriptionSession = null;

        var statusValue = status.ToString();

        var query = _session.Query<SubscriptionSession, SubscriptionSessionIndex>(x => x.SessionId == sessionId && x.Status == statusValue);

        if (_contextAccessor.HttpContext.User.Identity.IsAuthenticated)
        {
            var ownerId = CurrentUserId();

            subscriptionSession = await query.Where(x => x.OwnerId == ownerId).FirstOrDefaultAsync();
        }
        else
        {
            subscriptionSession = await query.Where(x => x.OwnerId == null).FirstOrDefaultAsync();

            // Don't trust the user, check for additional info.
            var ipAddress = (await _clientIPAddressAccessor.GetIPAddressAsync()).ToString();

            if (string.IsNullOrWhiteSpace(subscriptionSession?.IPAddress) ||
                subscriptionSession.IPAddress != ipAddress ||
                string.IsNullOrWhiteSpace(subscriptionSession?.AgentInfo) ||
                subscriptionSession.AgentInfo != _contextAccessor.HttpContext.Request.Headers.UserAgent)
            {
                // IMPORTANT: the saved session possibly belongs to someone else.
                // Do not use it.
                subscriptionSession = null;
            }
        }

        return subscriptionSession;
    }

    public async Task<SubscriptionSession> GetOrNewAsync(ContentItem subscriptionContentItem)
    {
        if (_contextAccessor.HttpContext.User.Identity.IsAuthenticated)
        {
            var ownerId = CurrentUserId();
            var status = nameof(SubscriptionSessionStatus.Pending);
            var modifiedUtc = _clock.UtcNow.AddDays(-1);

            var subscriptionSession = await _session
                .Query<SubscriptionSession, SubscriptionSessionIndex>(x => x.ContentItemVersionId == subscriptionContentItem.ContentItemVersionId && x.OwnerId == ownerId && x.Status == status && x.ModifiedUtc > modifiedUtc)
                .OrderByDescending(x => x.ModifiedUtc)
                .FirstOrDefaultAsync();

            if (subscriptionSession != null)
            {
                return subscriptionSession;
            }
        }

        return await NewAsync(subscriptionContentItem);
    }

    public async Task<SubscriptionSession> NewAsync(ContentItem subscriptionContentItem)
    {
        ArgumentNullException.ThrowIfNull(subscriptionContentItem);

        var now = _clock.UtcNow;

        var subscriptionSession = new SubscriptionSession()
        {
            SessionId = IdGenerator.GenerateId(),
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

        var initializingContext = new SubscriptionFlowInitializingContext(subscriptionSession, subscriptionContentItem);

        await _subscriptionHandlers.InvokeAsync((handler, context) => handler.InitializingAsync(context), initializingContext, _logger);

        var flow = new SubscriptionFlow(subscriptionSession, subscriptionContentItem);
        var initializedContext = new SubscriptionFlowInitializedContext(flow);

        subscriptionSession.CurrentStep = flow.GetFirstStep()?.Key;

        await _subscriptionHandlers.InvokeAsync((handler, context) => handler.InitializedAsync(context), initializedContext, _logger);

        return subscriptionSession;
    }

    public Task SaveAsync(SubscriptionSession session)
        => _session.SaveAsync(session);

    private string CurrentUserId()
        => _contextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
}
