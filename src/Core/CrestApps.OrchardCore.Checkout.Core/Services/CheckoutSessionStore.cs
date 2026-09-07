using System.Security.Claims;
using CrestApps.OrchardCore.Checkout.Core.Indexes;
using CrestApps.OrchardCore.Checkout.Handlers;
using CrestApps.OrchardCore.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Modules;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Checkout.Core.Services;

/// <summary>
/// The default YesSql-backed <see cref="ICheckoutSessionStore"/>. It enforces session ownership so an
/// anonymous checkout session can never be resumed by a different visitor, and runs the checkout handlers
/// when a new session is activated so features contribute their steps and billing items.
/// </summary>
public sealed class CheckoutSessionStore : ICheckoutSessionStore
{
    private static readonly GuestSessionTokenScope _guestTokenScope = new(
        CheckoutConstants.GuestTokenCookieName,
        CheckoutConstants.GuestTokenProtectorPurpose,
        TimeSpan.FromDays(30));

    private readonly IHttpContextAccessor _contextAccessor;
    private readonly IClientIPAddressAccessor _clientIPAddressAccessor;
    private readonly GuestSessionTokenManager _guestTokenManager;
    private readonly IEnumerable<ICheckoutHandler> _checkoutHandlers;
    private readonly ILogger<CheckoutSessionStore> _logger;
    private readonly IClock _clock;
    private readonly ISession _session;

    public CheckoutSessionStore(
        IHttpContextAccessor contextAccessor,
        IClientIPAddressAccessor clientIPAddressAccessor,
        GuestSessionTokenManager guestTokenManager,
        IEnumerable<ICheckoutHandler> checkoutHandlers,
        ILogger<CheckoutSessionStore> logger,
        IClock clock,
        ISession session)
    {
        _contextAccessor = contextAccessor;
        _clientIPAddressAccessor = clientIPAddressAccessor;
        _guestTokenManager = guestTokenManager;
        _checkoutHandlers = checkoutHandlers;
        _logger = logger;
        _clock = clock;
        _session = session;
    }

    /// <inheritdoc/>
    public Task<CheckoutSession> GetAsync(string sessionId, CancellationToken cancellationToken = default)
        => _session.Query<CheckoutSession, CheckoutSessionIndex>(x => x.SessionId == sessionId)
            .FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<CheckoutSession> GetAsync(string sessionId, CheckoutSessionStatus status, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        var query = _session.Query<CheckoutSession, CheckoutSessionIndex>(x => x.SessionId == sessionId && x.Status == status);

        if (_contextAccessor.HttpContext.User.Identity.IsAuthenticated)
        {
            var ownerId = CurrentUserId();

            return await query.Where(x => x.OwnerId == ownerId).FirstOrDefaultAsync(cancellationToken);
        }

        var checkoutSession = await query.Where(x => x.OwnerId == null).FirstOrDefaultAsync(cancellationToken);

        if (checkoutSession is null)
        {
            return null;
        }

        // IMPORTANT: Only the browser that started this checkout was given the ownership token, so this is
        // what keeps one visitor from resuming another's session and reading their contact details and
        // amounts. The IP address and user agent on the session are audit fields and prove nothing.
        if (!_guestTokenManager.Verify(_guestTokenScope, sessionId, checkoutSession.GuestTokenHash))
        {
            return null;
        }

        return checkoutSession;
    }

    /// <inheritdoc/>
    public Task<CheckoutSession> GetByReferenceAsync(string referenceType, string referenceId, string referenceVersionId = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(referenceType);
        ArgumentException.ThrowIfNullOrEmpty(referenceId);

        var query = _session.Query<CheckoutSession, CheckoutSessionIndex>(x => x.ReferenceType == referenceType && x.ReferenceId == referenceId);

        if (!string.IsNullOrEmpty(referenceVersionId))
        {
            query = query.Where(x => x.ReferenceVersionId == referenceVersionId);
        }

        return query.OrderByDescending(x => x.CreatedUtc).FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<CheckoutSession> NewAsync(string referenceType, string referenceId, string referenceVersionId = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(referenceType);

        cancellationToken.ThrowIfCancellationRequested();

        var checkoutSession = await GetNewSessionAsync(referenceType, referenceId, referenceVersionId);

        var activatingContext = new CheckoutFlowActivatingContext(checkoutSession);

        await _checkoutHandlers.InvokeAsync((handler, context) => handler.ActivatingAsync(context), activatingContext, _logger);

        var flow = new CheckoutFlow(checkoutSession);

        var activatedContext = new CheckoutFlowActivatedContext(flow);

        checkoutSession.CurrentStep = flow.GetFirstStep()?.Key;

        await _checkoutHandlers.InvokeAsync((handler, context) => handler.ActivatedAsync(context), activatedContext, _logger);

        return checkoutSession;
    }

    /// <inheritdoc/>
    public Task SaveAsync(CheckoutSession session, CancellationToken cancellationToken = default)
        => _session.SaveAsync(session, cancellationToken: cancellationToken);

    private async Task<CheckoutSession> GetNewSessionAsync(string referenceType, string referenceId, string referenceVersionId)
    {
        var now = _clock.UtcNow;

        var checkoutSession = new CheckoutSession
        {
            SessionId = IdGenerator.GenerateId(),
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            ReferenceVersionId = referenceVersionId,
            CreatedUtc = now,
            ModifiedUtc = now,
            Status = CheckoutSessionStatus.Pending,
        };

        if (_contextAccessor.HttpContext.User.Identity.IsAuthenticated)
        {
            checkoutSession.OwnerId = CurrentUserId();
        }
        else
        {
            // Recorded for audit only; the token below is what decides who may resume the session.
            checkoutSession.IPAddress = (await _clientIPAddressAccessor.GetIPAddressAsync()).ToString();
            checkoutSession.AgentInfo = _contextAccessor.HttpContext.Request.Headers.UserAgent;
            checkoutSession.GuestTokenHash = _guestTokenManager.Issue(_guestTokenScope, checkoutSession.SessionId);
        }

        return checkoutSession;
    }

    private string CurrentUserId()
        => _contextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
}
