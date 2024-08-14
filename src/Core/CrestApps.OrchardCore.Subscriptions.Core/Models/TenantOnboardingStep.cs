namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

public sealed class TenantOnboardingStep
{
    public string[] Domains { get; set; }

    public string[] LocalDomains { get; set; }

    public string TenantName { get; set; }

    public string TenantTitle { get; set; }

    public string AdminUsername { get; set; }

    public string AdminEmail { get; set; }

    public string AdminPassword { get; set; }

    public string Prefix { get; set; }

    public string[] GetDomains()
        => (Domains ?? []).Concat(LocalDomains ?? [])
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}
