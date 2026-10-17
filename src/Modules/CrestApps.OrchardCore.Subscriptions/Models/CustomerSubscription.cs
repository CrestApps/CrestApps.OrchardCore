namespace CrestApps.OrchardCore.Subscriptions.Models;

/// <summary>
/// A subscription as its owner sees it, one row of their own plans (My Plans).
/// </summary>
/// <param name="Subscription">The customer's agreement.</param>
/// <param name="IsCurrent">Whether the agreement still grants access now.</param>
public sealed record CustomerSubscription(Subscription Subscription, bool IsCurrent);
