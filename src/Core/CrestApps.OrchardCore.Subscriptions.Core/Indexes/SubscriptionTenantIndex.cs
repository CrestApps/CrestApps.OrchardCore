using YesSql.Indexes;

namespace CrestApps.OrchardCore.Subscriptions.Core.Indexes;

/// <summary>
/// Indexes tenant onboarding information collected during a subscription session.
/// </summary>
public sealed class SubscriptionTenantIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the tenant name requested by the subscriber.
    /// </summary>
    public string TenantName { get; set; }

    /// <summary>
    /// Gets or sets the setup recipe selected for the tenant.
    /// </summary>
    public string Recipe { get; set; }

    /// <summary>
    /// Gets or sets the subscription session identifier that contains the tenant onboarding data.
    /// </summary>
    public string SessionId { get; set; }
}
