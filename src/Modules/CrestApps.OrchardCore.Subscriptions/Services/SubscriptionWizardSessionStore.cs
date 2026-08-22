using CrestApps.OrchardCore.Subscriptions;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using CrestApps.OrchardCore.Wizard;
using CrestApps.OrchardCore.Wizard.Core.Services;
using CrestApps.OrchardCore.Wizard.Handlers;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Subscriptions.Services;

internal sealed class SubscriptionWizardSessionStore : IWizardSessionStore
{
    private readonly WizardSessionStore _defaultWizardSessionStore;
    private readonly ISubscriptionSessionStore _subscriptionSessionStore;
    private readonly IContentManager _contentManager;
    private readonly IEnumerable<IWizardHandler> _wizardHandlers;
    private readonly ILogger<SubscriptionWizardSessionStore> _logger;

    public SubscriptionWizardSessionStore(
        WizardSessionStore defaultWizardSessionStore,
        ISubscriptionSessionStore subscriptionSessionStore,
        IContentManager contentManager,
        IEnumerable<IWizardHandler> wizardHandlers,
        ILogger<SubscriptionWizardSessionStore> logger)
    {
        _defaultWizardSessionStore = defaultWizardSessionStore;
        _subscriptionSessionStore = subscriptionSessionStore;
        _contentManager = contentManager;
        _wizardHandlers = wizardHandlers;
        _logger = logger;
    }

    public async Task<WizardSession> GetAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var wizardSession = await _defaultWizardSessionStore.GetAsync(sessionId, cancellationToken);

        if (wizardSession != null)
        {
            return wizardSession;
        }

        var subscriptionSession = await _subscriptionSessionStore.GetAsync(sessionId);

        return MapSubscriptionSession(subscriptionSession);
    }

    public async Task<WizardSession> GetAsync(
        string sessionId,
        WizardSessionStatus status,
        CancellationToken cancellationToken = default)
    {
        var wizardSession = await _defaultWizardSessionStore.GetAsync(sessionId, status, cancellationToken);

        if (wizardSession != null)
        {
            return wizardSession;
        }

        var subscriptionStatus = status switch
        {
            WizardSessionStatus.Completed => SubscriptionSessionStatus.Completed,
            WizardSessionStatus.Canceled => SubscriptionSessionStatus.Canceled,
            _ => SubscriptionSessionStatus.Pending,
        };

        var subscriptionSession = await _subscriptionSessionStore.GetAsync(sessionId, subscriptionStatus);

        return MapSubscriptionSession(subscriptionSession);
    }

    public async Task<WizardSession> NewAsync(
        string wizardType,
        string definitionId = null,
        string definitionVersionId = null,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(wizardType, SubscriptionConstants.WizardType, StringComparison.OrdinalIgnoreCase))
        {
            return await _defaultWizardSessionStore.NewAsync(wizardType, definitionId, definitionVersionId, cancellationToken);
        }

        var subscriptionContentItem = !string.IsNullOrWhiteSpace(definitionVersionId)
            ? await _contentManager.GetVersionAsync(definitionVersionId)
            : await _contentManager.GetAsync(definitionId, VersionOptions.Published);

        if (subscriptionContentItem == null)
        {
            throw new InvalidOperationException($"The subscription definition '{definitionId ?? definitionVersionId}' could not be found.");
        }

        var subscriptionSession = await _subscriptionSessionStore.NewAsync(subscriptionContentItem);
        var wizardSession = MapSubscriptionSession(subscriptionSession);
        var flow = new WizardFlow(wizardSession);

        await _wizardHandlers.InvokeAsync((handler, context) => handler.ActivatedAsync(context), new WizardFlowActivatedContext(flow), _logger);

        return wizardSession;
    }

    public async Task SaveAsync(WizardSession session, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(session?.WizardType, SubscriptionConstants.WizardType, StringComparison.OrdinalIgnoreCase))
        {
            await _defaultWizardSessionStore.SaveAsync(session, cancellationToken);

            return;
        }

        var subscriptionSession = await _subscriptionSessionStore.GetAsync(session.SessionId);

        if (subscriptionSession == null)
        {
            throw new InvalidOperationException($"The subscription session '{session.SessionId}' could not be found.");
        }

        SubscriptionWizardSessionMapper.CopyToSubscriptionSession(session, subscriptionSession);

        await _subscriptionSessionStore.SaveAsync(subscriptionSession);
    }

    private static WizardSession MapSubscriptionSession(SubscriptionSession subscriptionSession)
    {
        if (subscriptionSession == null)
        {
            return null;
        }

        var wizardSession = SubscriptionWizardSessionMapper.ToWizardSession(subscriptionSession);

        wizardSession.Properties[WizardConstants.SuppressDefaultChromePropertyKey] = true;

        return wizardSession;
    }
}
