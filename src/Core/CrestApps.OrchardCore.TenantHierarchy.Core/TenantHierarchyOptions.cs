using CrestApps.OrchardCore.TenantHierarchy.Models;

namespace CrestApps.OrchardCore.TenantHierarchy.Core;

/// <summary>
/// Holds the host configuration of the tenant hierarchy, read from the <c>TenantHierarchy</c> section of the
/// application configuration. Tenants cannot change it.
/// </summary>
public sealed class TenantHierarchyOptions
{
    /// <summary>
    /// The configuration section that holds these options.
    /// </summary>
    public const string SectionName = "TenantHierarchy";

    /// <summary>
    /// Gets or sets the platform domain that parent hosts are built on, for example <c>platform.com</c>. A parent
    /// with the slug <c>firma</c> gets the host <c>firma.platform.com</c>. When it is empty, the host of the request
    /// to the Default tenant is used.
    /// </summary>
    public string PlatformDomain { get; set; }

    /// <summary>
    /// Gets or sets the scheme of tenant addresses. When it is empty, <c>https</c> is used, except in development
    /// where the scheme of the current request is used.
    /// </summary>
    public string Scheme { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the authentication and antiforgery cookies of parent and child tenants
    /// get the <c>__Host-</c> name prefix, which requires HTTPS. It is required in production, because a child tenant
    /// shares the domain of its parent. When it is not set, it is on everywhere except in development, where tenants
    /// are often served over plain HTTP.
    /// </summary>
    public bool? UseHostPrefixedCookies { get; set; }

    /// <summary>
    /// Gets or sets the database pools child tenants can be placed on, by name.
    /// </summary>
    public Dictionary<string, DatabasePoolOptions> DatabasePools { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets the slugs no tenant may use, in addition to the built-in list.
    /// </summary>
    public string[] ReservedSlugs { get; set; } = [];

    /// <summary>
    /// Gets or sets the outbound request guard options.
    /// </summary>
    public TenantHierarchyEgressOptions Egress { get; set; } = new();
}
