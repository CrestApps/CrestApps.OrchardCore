namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

/// <summary>
/// The editor for what a subscription plan grants.
/// </summary>
public class SubscriptionEntitlementPartViewModel
{
    /// <summary>
    /// Gets or sets the roles available on the tenant, each marked as granted by this plan or not.
    /// </summary>
    public IList<SubscriptionEntitlementRoleEntry> Roles { get; set; } = [];
}

/// <summary>
/// One role a plan may grant.
/// </summary>
public sealed class SubscriptionEntitlementRoleEntry
{
    /// <summary>
    /// Gets or sets the role name.
    /// </summary>
    public string RoleName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the plan grants the role.
    /// </summary>
    public bool IsSelected { get; set; }
}
