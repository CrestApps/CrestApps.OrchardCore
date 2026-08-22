using CrestApps.OrchardCore.Subscriptions;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Wizard;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Subscriptions.Services;

internal sealed class SubscriptionWizardFlowContext
{
    public SubscriptionWizardFlowContext(
        WizardSession wizardSession,
        SubscriptionSession subscriptionSession,
        ContentItem subscriptionContentItem)
    {
        ArgumentNullException.ThrowIfNull(wizardSession);
        ArgumentNullException.ThrowIfNull(subscriptionSession);
        ArgumentNullException.ThrowIfNull(subscriptionContentItem);

        WizardSession = wizardSession;
        SubscriptionSession = subscriptionSession;
        SubscriptionContentItem = subscriptionContentItem;
        Flow = new SubscriptionFlow(subscriptionSession, subscriptionContentItem);
    }

    public WizardSession WizardSession { get; }

    public SubscriptionSession SubscriptionSession { get; }

    public ContentItem SubscriptionContentItem { get; }

    public SubscriptionFlow Flow { get; }

    public void SyncToWizardSession()
        => SubscriptionWizardSessionMapper.CopyToWizardSession(SubscriptionSession, WizardSession);
}
