using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Subscriptions.Core;

/// <summary>
/// Defines permissions used by the subscriptions module.
/// </summary>
public static class SubscriptionPermissions
{
    /// <summary>
    /// Allows a user to manage subscription settings.
    /// </summary>
    public static readonly Permission ManageSubscriptionSettings = new(
        "ManageSubscriptionSettings",
        LocalizationSource.Create("Manage subscriptions settings", typeof(SubscriptionPermissions)),
        isSecurityCritical: true);

    /// <summary>
    /// Allows a user to manage all subscription sessions.
    /// </summary>
    public static readonly Permission ManageSubscriptions = new(
        "ManageSubscriptions",
        LocalizationSource.Create("Manage subscriptions", typeof(SubscriptionPermissions)));

    /// <summary>
    /// Allows a user to manage subscription sessions they own and is implied by <see cref="ManageSubscriptions"/>.
    /// </summary>
    public static readonly Permission ManageOwnSubscriptions = new(
        "ManageOwnSubscriptions",
        LocalizationSource.Create("Manage own subscriptions", typeof(SubscriptionPermissions)),
        [ManageSubscriptions]);

    /// <summary>
    /// Allows a user to create installment plans for customers, take their down payment by card, and manage the
    /// plans' payments. It charges customers' saved cards, so it is granted to administrators only by default.
    /// </summary>
    public static readonly Permission ManageInstallmentPlans = new(
        "ManageInstallmentPlans",
        LocalizationSource.Create("Manage installment plans and charge customers' saved cards", typeof(SubscriptionPermissions)),
        isSecurityCritical: true);
}
