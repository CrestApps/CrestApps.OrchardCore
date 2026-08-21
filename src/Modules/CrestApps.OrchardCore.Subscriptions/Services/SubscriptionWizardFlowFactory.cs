using OrchardCore.ContentManagement;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Wizard;

namespace CrestApps.OrchardCore.Subscriptions.Services;

internal sealed class SubscriptionWizardFlowFactory
{
    private readonly IContentManager _contentManager;

    public SubscriptionWizardFlowFactory(
        IContentManager contentManager)
    {
        _contentManager = contentManager;
    }

    public async Task<SubscriptionWizardFlowContext> CreateAsync(WizardFlow flow)
    {
        ArgumentNullException.ThrowIfNull(flow);

        return await CreateAsync(flow.Session as WizardSession);
    }

    public async Task<SubscriptionWizardFlowContext> CreateAsync(WizardSession wizardSession)
    {
        if (!IsSubscriptionWizard(wizardSession))
        {
            return null;
        }

        var subscriptionContentItem = await GetSubscriptionContentItemAsync(wizardSession);

        if (subscriptionContentItem == null)
        {
            return null;
        }

        var subscriptionSession = SubscriptionWizardSessionMapper.ToSubscriptionSession(wizardSession, subscriptionContentItem);

        return new SubscriptionWizardFlowContext(wizardSession, subscriptionSession, subscriptionContentItem);
    }

    public async Task<ContentItem> GetSubscriptionContentItemAsync(WizardSession wizardSession)
    {
        if (!IsSubscriptionWizard(wizardSession))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(wizardSession.DefinitionVersionId))
        {
            var version = await _contentManager.GetVersionAsync(wizardSession.DefinitionVersionId);

            if (version != null)
            {
                return version;
            }
        }

        if (!string.IsNullOrWhiteSpace(wizardSession.DefinitionId))
        {
            return await _contentManager.GetAsync(wizardSession.DefinitionId, VersionOptions.Published);
        }

        return null;
    }

    private static bool IsSubscriptionWizard(WizardSession wizardSession)
        => wizardSession != null &&
        string.Equals(wizardSession.WizardType, SubscriptionConstants.WizardType, StringComparison.OrdinalIgnoreCase);
}
