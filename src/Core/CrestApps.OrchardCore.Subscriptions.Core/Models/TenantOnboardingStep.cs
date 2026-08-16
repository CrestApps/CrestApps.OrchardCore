namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

/// <summary>
/// Represents tenant setup data collected during a subscription onboarding step.
/// </summary>
public sealed class TenantOnboardingStep
{
    /// <summary>
    /// Gets or sets the custom domains requested for the tenant.
    /// </summary>
    public string[] Domains { get; set; }

    /// <summary>
    /// Gets or sets the generated local domains requested for the tenant.
    /// </summary>
    public string[] LocalDomains { get; set; }

    /// <summary>
    /// Gets or sets the unique tenant name used by Orchard Core.
    /// </summary>
    public string TenantName { get; set; }

    /// <summary>
    /// Gets or sets the display title for the tenant site.
    /// </summary>
    public string TenantTitle { get; set; }

    /// <summary>
    /// Gets or sets the administrator user name for the tenant setup.
    /// </summary>
    public string AdminUsername { get; set; }

    /// <summary>
    /// Gets or sets the administrator email address for the tenant setup.
    /// </summary>
    public string AdminEmail { get; set; }

    /// <summary>
    /// Gets or sets the administrator password value used during tenant setup.
    /// </summary>
    public string AdminPassword { get; set; }

    /// <summary>
    /// Gets or sets the URL prefix assigned to the tenant.
    /// </summary>
    public string Prefix { get; set; }

    /// <summary>
    /// Gets or sets the setup recipe name used to initialize the tenant.
    /// </summary>
    public string RecipeName { get; set; }

    /// <summary>
    /// Gets or sets the feature profile applied to the tenant.
    /// </summary>
    public string FeatureProfile { get; set; }

    /// <summary>
    /// Gets the distinct set of custom and local domains assigned to the tenant.
    /// </summary>
    /// <returns>The distinct tenant domains, compared without case sensitivity.</returns>
    public string[] GetDomains()
        => (Domains ?? []).Concat(LocalDomains ?? [])
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}
