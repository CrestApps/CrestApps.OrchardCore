namespace CrestApps.OrchardCore.Subscriptions.Core;

/// <summary>
/// The checkout reference that identifies a checkout as the purchase of a subscription plan.
/// </summary>
/// <remarks>
/// The checkout framework is deliberately domain-neutral, so the reference vocabulary is owned by the
/// consumer rather than by the checkout. A subscription checkout references the plan's content item id;
/// the version id records which version of the plan the customer actually agreed to, so editing a plan
/// afterwards never rewrites what somebody already bought.
/// </remarks>
public static class SubscriptionCheckout
{
    /// <summary>
    /// The <c>ReferenceType</c> used by a checkout that buys a subscription plan.
    /// </summary>
    public const string ReferenceType = "Subscription";

    /// <summary>
    /// Determines whether a checkout reference type identifies a subscription purchase.
    /// </summary>
    /// <param name="referenceType">The checkout session's reference type.</param>
    /// <returns><see langword="true"/> when the checkout buys a subscription plan.</returns>
    public static bool IsSubscriptionReference(string referenceType)
        => string.Equals(referenceType, ReferenceType, StringComparison.OrdinalIgnoreCase);
}
