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

    /// <summary>
    /// Gets or sets the database provider every provisioned site is created with.
    /// </summary>
    /// <remarks>
    /// A site cannot be created without one — setup refuses outright — and the buyer is in no position to
    /// choose it, so it is configured once by the operator and applied to every site sold. When it is left
    /// empty the sites are created on <c>Sqlite</c>, which needs no server and gives each site its own file.
    /// </remarks>
    public string DatabaseProvider { get; set; }

    /// <summary>
    /// Gets or sets the connection string provisioned sites are created with. Not needed for
    /// <c>Sqlite</c>, which stores each site under its own tenant folder.
    /// </summary>
    public string ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the schema provisioned sites are created in, when the provider supports one.
    /// </summary>
    public string Schema { get; set; }

    /// <summary>
    /// Gets the database provider to create a site with, falling back to the one that works without any
    /// server to configure.
    /// </summary>
    public string GetDatabaseProviderOrDefault()
        => string.IsNullOrWhiteSpace(DatabaseProvider) ? DefaultDatabaseProvider : DatabaseProvider.Trim();

    /// <summary>
    /// The provider used when the operator has configured none. It needs no connection string and keeps
    /// each site's data in its own file, so selling a site works before anything is configured.
    /// </summary>
    public const string DefaultDatabaseProvider = "Sqlite";
}
