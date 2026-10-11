using System.Security.Claims;
using CrestApps.OrchardCore.Wizard.Core.Indexes;
using CrestApps.OrchardCore.Wizard.Handlers;
using CrestApps.OrchardCore.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Modules;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Wizard.Core.Services;

/// <summary>
/// The default YesSql-backed <see cref="IWizardSessionStore"/>. It enforces session ownership so an
/// anonymous wizard session can never be resumed by a different visitor, and runs the wizard handlers when a
/// new session is activated so features contribute their steps.
/// </summary>
public sealed class WizardSessionStore : IWizardSessionStore
{
    private static readonly GuestSessionTokenScope _guestTokenScope = new(
        "wizard_owner",
        "CrestApps.OrchardCore.Wizard.GuestOwnership.v1",
        TimeSpan.FromDays(30));

    private readonly IHttpContextAccessor _contextAccessor;
    private readonly GuestSessionTokenManager _guestTokenManager;
    private readonly IClientIPAddressAccessor _clientIPAddressAccessor;
    private readonly IEnumerable<IWizardHandler> _wizardHandlers;
    private readonly ILogger<WizardSessionStore> _logger;
    private readonly IClock _clock;
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="WizardSessionStore"/> class.
    /// </summary>
    /// <param name="contextAccessor">The HTTP context accessor.</param>
    /// <param name="clientIPAddressAccessor">The client IP address accessor.</param>
    /// <param name="wizardHandlers">The registered wizard handlers.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="session">The YesSql session.</param>
    public WizardSessionStore(
        IHttpContextAccessor contextAccessor,
        IClientIPAddressAccessor clientIPAddressAccessor,
        GuestSessionTokenManager guestTokenManager,
        IEnumerable<IWizardHandler> wizardHandlers,
        ILogger<WizardSessionStore> logger,
        IClock clock,
        ISession session)
    {
        _contextAccessor = contextAccessor;
        _clientIPAddressAccessor = clientIPAddressAccessor;
        _guestTokenManager = guestTokenManager;
        _wizardHandlers = wizardHandlers;
        _logger = logger;
        _clock = clock;
        _session = session;
    }

    /// <inheritdoc/>
    public Task<WizardSession> GetAsync(string sessionId, CancellationToken cancellationToken = default)
        => _session.Query<WizardSession, WizardSessionIndex>(x => x.SessionId == sessionId)
            .FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<WizardSession> GetAsync(string sessionId, WizardSessionStatus status, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        var query = _session.Query<WizardSession, WizardSessionIndex>(x => x.SessionId == sessionId && x.Status == status);

        if (_contextAccessor.HttpContext.User.Identity.IsAuthenticated)
        {
            var ownerId = CurrentUserId();

            return await query.Where(x => x.OwnerId == ownerId).FirstOrDefaultAsync(cancellationToken);
        }

        var wizardSession = await query.Where(x => x.OwnerId == null).FirstOrDefaultAsync(cancellationToken);

        if (wizardSession is null)
        {
            return null;
        }

        // IMPORTANT: Only the browser that started this session was given the ownership token, so this is
        // what keeps one visitor from resuming another's session and reading what is on it. The IP address
        // and user agent on the session are audit fields and prove nothing.
        if (!_guestTokenManager.Verify(_guestTokenScope, sessionId, wizardSession.GuestTokenHash))
        {
            return null;
        }

        return wizardSession;
    }

    /// <inheritdoc/>
    public async Task<WizardSession> NewAsync(string wizardType, string definitionId = null, string definitionVersionId = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(wizardType);

        cancellationToken.ThrowIfCancellationRequested();

        var wizardSession = await GetNewSessionAsync(wizardType, definitionId, definitionVersionId);

        var activatingContext = new WizardFlowActivatingContext(wizardSession);

        await _wizardHandlers.InvokeAsync((handler, context) => handler.ActivatingAsync(context), activatingContext, _logger);

        var flow = new WizardFlow(wizardSession);

        var activatedContext = new WizardFlowActivatedContext(flow);

        wizardSession.CurrentStep = flow.GetFirstStep()?.Key;

        await _wizardHandlers.InvokeAsync((handler, context) => handler.ActivatedAsync(context), activatedContext, _logger);

        return wizardSession;
    }

    /// <inheritdoc/>
    public Task SaveAsync(WizardSession session, CancellationToken cancellationToken = default)
        => _session.SaveAsync(session, cancellationToken: cancellationToken);

    private async Task<WizardSession> GetNewSessionAsync(string wizardType, string definitionId, string definitionVersionId)
    {
        var now = _clock.UtcNow;

        var wizardSession = new WizardSession
        {
            SessionId = IdGenerator.GenerateId(),
            WizardType = wizardType,
            DefinitionId = definitionId,
            DefinitionVersionId = definitionVersionId,
            CreatedUtc = now,
            ModifiedUtc = now,
            Status = WizardSessionStatus.Pending,
        };

        if (_contextAccessor.HttpContext.User.Identity.IsAuthenticated)
        {
            wizardSession.OwnerId = CurrentUserId();
        }
        else
        {
            // Recorded for audit only; the token below is what decides who may resume the session.
            wizardSession.IPAddress = (await _clientIPAddressAccessor.GetIPAddressAsync()).ToString();
            wizardSession.AgentInfo = _contextAccessor.HttpContext.Request.Headers.UserAgent;
            wizardSession.GuestTokenHash = _guestTokenManager.Issue(_guestTokenScope, wizardSession.SessionId);
        }

        return wizardSession;
    }

    private string CurrentUserId()
        => _contextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
}
