namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

/// <summary>
/// Represents tenant onboarding settings used by subscription flows that provision sites.
/// </summary>
public sealed class SubscriptionOnboardingSettings
{
    /// <summary>
    /// The placeholder replaced with the generated tenant key in local domain templates.
    /// </summary>
    public const string TenantKeyVariable = "{tenantKey}";

    /// <summary>
    /// The placeholder replaced with the current request host in local domain templates.
    /// </summary>
    public const string CurrentHostVariable = "{currentHost}";

    /// <summary>
    /// Gets or sets a value indicating whether subscribers may provide custom domains.
    /// </summary>
    public bool AllowCustomDomains { get; set; }

    /// <summary>
    /// Gets or sets how local domains are generated for provisioned tenants.
    /// </summary>
    public LocalDomainType LocalDomainType { get; set; }

    /// <summary>
    /// Gets or sets the template used to generate local domains for provisioned tenants.
    /// </summary>
    public string LocalDomainTemplate { get; set; }
}
