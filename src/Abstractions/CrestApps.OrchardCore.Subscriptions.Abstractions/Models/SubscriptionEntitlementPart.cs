using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Subscriptions.Models;

/// <summary>
/// Attaches to a subscription plan the things a subscriber is entitled to while their subscription is
/// current.
/// </summary>
/// <remarks>
/// The entitlement belongs on the plan rather than in a site setting because different plans grant
/// different things: that is the whole point of having more than one plan. A site-wide role list can only
/// express "everybody who ever subscribes gets this", which cannot describe a bronze and a gold tier.
///
/// The entitlements are copied onto the subscription when it is created, so editing the plan later does not
/// silently change what an existing subscriber was sold.
/// </remarks>
public sealed class SubscriptionEntitlementPart : ContentPart
{
    /// <summary>
    /// Gets or sets the names of the roles a subscriber holds while the subscription is current.
    /// </summary>
    public string[] RoleNames { get; set; }
}
