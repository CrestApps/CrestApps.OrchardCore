namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

/// <summary>
/// Represents role assignment settings for users created during subscription registration.
/// </summary>
public sealed class SubscriptionRoleSettings
{
    /// <summary>
    /// Gets or sets the role names assigned to newly created subscription users.
    /// </summary>
    public string[] RoleNames { get; set; }
}
