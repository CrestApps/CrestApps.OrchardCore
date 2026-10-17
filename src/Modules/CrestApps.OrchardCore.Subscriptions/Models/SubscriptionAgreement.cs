namespace CrestApps.OrchardCore.Subscriptions.Models;

/// <summary>
/// A subscription agreement as one row of the administration agreements report.
/// </summary>
/// <remarks>
/// The rows are not built from <see cref="Subscription"/> itself: a content type with the Subscription stereotype
/// is displayed as a shape of that name, so a <c>Subscription.SummaryAdmin</c> template would also replace how
/// those content items look in the content list.
/// </remarks>
/// <param name="Subscription">The agreement.</param>
public sealed record SubscriptionAgreement(Subscription Subscription);
