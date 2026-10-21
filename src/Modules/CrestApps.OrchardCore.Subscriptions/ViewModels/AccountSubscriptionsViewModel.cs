using CrestApps.OrchardCore.Subscriptions.Models;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

/// <summary>
/// The signed-in customer's subscriptions on the storefront account. It is the model of the
/// <c>AccountSubscriptions</c> shape, so it stays unsealed for the shape's proxy.
/// </summary>
public class AccountSubscriptionsViewModel
{
    /// <summary>
    /// Gets or sets the customer's subscriptions, the ones still running first.
    /// </summary>
    public IReadOnlyList<CustomerSubscription> Subscriptions { get; set; } = [];
}
