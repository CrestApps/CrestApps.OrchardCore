using OrchardCore.Users.ViewModels;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

/// <summary>
/// Represents the role selection settings used for subscription users.
/// </summary>
public class SubscriptionRoleSettingsViewModel
{
    /// <summary>
    /// Gets or sets the roles available for subscription role configuration.
    /// </summary>
    public RoleEntry[] Roles { get; set; }
}
