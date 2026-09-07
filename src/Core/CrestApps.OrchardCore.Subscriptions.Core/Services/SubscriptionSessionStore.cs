using CrestApps.OrchardCore.Core.Services;
using System.Security.Claims;
using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

/// <summary>
/// Persists subscription sessions and verifies ownership for session resume operations.
/// </summary>
public sealed class SubscriptionSessionStore : ISubscriptionSessionStore
{
    private static readonly GuestSessionTokenScope _guestTokenScope = new(
        "subscription_owner",
        "CrestApps.OrchardCore.Subscriptions.GuestOwnership.v1",
        TimeSpan.FromDays(30));

    private readonly Microsoft.AspNetCore.Http.IHttpContextAccessor _contextAccessor;
    private readonly GuestSessionTokenManager _guestTokenManager;
    private readonly IClientIPAddressAccessor _clientIPAddressAccessor;
    private readonly IEnumerable<ISubscriptionHandler> _subscriptionHandlers;
    private readonly ILogger<SubscriptionSessionStore> _logger;
    private readonly IClock _clock;
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionSessionStore"/> class.
    /// </summary>
    /// <param name="contextAccessor">The HTTP context accessor used to identify the current caller.</param>
    /// <param name="clientIPAddressAccessor">The client IP address accessor used to record an audit trail for anonymous sessions.</param>
    /// <param name="guestTokenManager">The manager that issues and verifies a guest's ownership token.</param>
    /// <param name="subscriptionHandlers">The subscription handlers invoked while creating a new flow.</param>
    /// <param name="logger">The logger used when invoking subscription handlers.</param>
    /// <param name="clock">The clock used to stamp new sessions.</param>
    /// <param name="session">The YesSql session used to query and save subscription sessions.</param>
    public SubscriptionSessionStore(
        Microsoft.AspNetCore.Http.IHttpContextAccessor contextAccessor,
        IClientIPAddressAccessor clientIPAddressAccessor,
        GuestSessionTokenManager guestTokenManager,
        IEnumerable<ISubscriptionHandler> subscriptionHandlers,
        ILogger<SubscriptionSessionStore> logger,
        IClock clock,
        ISession session)
    {
        _contextAccessor = contextAccessor;
        _clientIPAddressAccessor = clientIPAddressAccessor;
        _guestTokenManager = guestTokenManager;
        _subscriptionHandlers = subscriptionHandlers;
        _logger = logger;
        _clock = clock;
        _session = session;
    }

    /// <summary>
    /// Gets a subscription session by its session identifier without checking status or ownership.
    /// </summary>
    /// <param name="sessionId">The subscription session identifier.</param>
    /// <returns>The matching subscription session, or <see langword="null"/> when none exists.</returns>
    public Task<SubscriptionSession> GetAsync(string sessionId)
        => _session.Query<SubscriptionSession, SubscriptionSessionIndex>(x => x.SessionId == sessionId)
        .FirstOrDefaultAsync();

    /// <summary>
    /// Gets a subscription session by its session identifier and status when it belongs to the current caller.
    /// </summary>
    /// <param name="sessionId">The subscription session identifier.</param>
    /// <param name="status">The required session status.</param>
    /// <returns>The matching subscription session, or <see langword="null"/> when none exists or ownership validation fails.</returns>
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
        if (subscriptionSession is null)
        {
            return null;
        }

        // IMPORTANT: Only the browser that started this session was given the ownership token, so this is
        // what keeps one visitor from resuming another's session and reading what is on it. The IP address
        // and user agent on the session are audit fields and prove nothing.
        if (!_guestTokenManager.Verify(_guestTokenScope, sessionId, subscriptionSession.GuestTokenHash))
        {
            // IMPORTANT: The saved session may belong to another user. Do not it.
            return null;
        }

        return subscriptionSession;
    }

    /// <summary>
    /// Creates a new pending subscription session for the specified subscription content item.
    /// </summary>
    /// <param name="subscriptionContentItem">The subscription content item being purchased.</param>
    /// <returns>The initialized subscription session.</returns>
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

    /// <summary>
    /// Saves the specified subscription session.
    /// </summary>
    /// <param name="session">The subscription session to save.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
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
            // Recorded for audit only; the token below is what decides who may resume the session.
            subscriptionSession.IPAddress = (await _clientIPAddressAccessor.GetIPAddressAsync()).ToString();
            subscriptionSession.AgentInfo = _contextAccessor.HttpContext.Request.Headers.UserAgent;
            subscriptionSession.GuestTokenHash = _guestTokenManager.Issue(_guestTokenScope, subscriptionSession.SessionId);
        }

        return subscriptionSession;
    }

    private string CurrentUserId()
        => _contextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
}
