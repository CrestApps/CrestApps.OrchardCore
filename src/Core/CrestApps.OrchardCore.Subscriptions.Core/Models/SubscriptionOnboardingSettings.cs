namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

public sealed class SubscriptionOnboardingSettings
{
    public const string TenantKeyVariable = "{tenantKey}";

    public const string CurrentHostVariable = "{currentHost}";

    public bool AllowCustomDomains { get; set; }

    public LocalDomainType LocalDomainType { get; set; }

    public string LocalDomainTemplate { get; set; }
}
