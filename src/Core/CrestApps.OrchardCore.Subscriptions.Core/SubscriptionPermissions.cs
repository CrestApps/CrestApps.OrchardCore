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
    public static readonly Permission ManageSubscriptionSettings = new("ManageSubscriptionSettings", "Manage subscriptions settings");

    /// <summary>
    /// Allows a user to manage all subscription sessions.
    /// </summary>
    public static readonly Permission ManageSubscriptions = new("ManageSubscriptions", "Manage subscriptions");

    /// <summary>
    /// Allows a user to manage subscription sessions they own and is implied by <see cref="ManageSubscriptions"/>.
    /// </summary>
    public static readonly Permission ManageOwnSubscriptions = new("ManageOwnSubscriptions", "Manage own subscriptions", [ManageSubscriptions]);
}
