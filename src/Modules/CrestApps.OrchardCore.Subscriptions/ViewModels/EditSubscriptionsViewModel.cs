using CrestApps.OrchardCore.Subscriptions.Core.Models;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

/// <summary>
/// Represents the subscriptions selected or edited during a subscription flow.
/// </summary>
public class EditSubscriptionsViewModel
{
    /// <summary>
    /// Gets or sets the subscription entries included in the edit form.
    /// </summary>
    public List<SubscriptionInfo> Subscriptions { get; set; }
}
