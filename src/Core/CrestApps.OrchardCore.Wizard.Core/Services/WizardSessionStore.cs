using System.Security.Claims;
using CrestApps.OrchardCore.Wizard.Core.Indexes;
using CrestApps.OrchardCore.Wizard.Handlers;
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
    private readonly IHttpContextAccessor _contextAccessor;
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
        IEnumerable<IWizardHandler> wizardHandlers,
        ILogger<WizardSessionStore> logger,
        IClock clock,
        ISession session)
    {
        _contextAccessor = contextAccessor;
        _clientIPAddressAccessor = clientIPAddressAccessor;
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

        var ipAddress = (await _clientIPAddressAccessor.GetIPAddressAsync()).ToString();

        if (string.IsNullOrWhiteSpace(wizardSession?.IPAddress) ||
            wizardSession.IPAddress != ipAddress ||
            string.IsNullOrWhiteSpace(wizardSession?.AgentInfo) ||
            wizardSession.AgentInfo != _contextAccessor.HttpContext.Request.Headers.UserAgent)
        {
            // IMPORTANT: The saved session may belong to another visitor. Do not return it.
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
            wizardSession.IPAddress = (await _clientIPAddressAccessor.GetIPAddressAsync()).ToString();
            wizardSession.AgentInfo = _contextAccessor.HttpContext.Request.Headers.UserAgent;
        }

        return wizardSession;
    }

    private string CurrentUserId()
        => _contextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
}
