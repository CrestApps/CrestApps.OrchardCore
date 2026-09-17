using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.ContactCenter.FeatureActivationTests.DependencyInjection;

/// <summary>
/// Holds the rendered service collection of each tenant the host builds, so a test can read what a
/// feature profile actually registered.
/// </summary>
/// <remarks>
/// Orchard Core hands a tenant's <see cref="IServiceCollection"/> only to the module startups that
/// configure it; nothing exposes it afterwards. The capture is therefore written from a startup that
/// runs inside the tenant, keyed by tenant name because several tenants exist at once.
/// </remarks>
internal static class TenantServiceCollectionCapture
{
    private static readonly ConcurrentDictionary<string, string> _snapshotsByTenant = new(StringComparer.Ordinal);

    /// <summary>
    /// Records the rendered descriptors of a tenant's service collection.
    /// </summary>
    /// <param name="tenantName">The tenant the collection belongs to.</param>
    /// <param name="services">The tenant's service collection.</param>
    public static void Capture(string tenantName, IServiceCollection services)
    {
        ArgumentException.ThrowIfNullOrEmpty(tenantName);
        ArgumentNullException.ThrowIfNull(services);

        // Rendered now rather than holding the collection: it keeps being mutated after this runs,
        // and the point of the snapshot is what the modules registered.
        _snapshotsByTenant[tenantName] = ServiceDescriptorSnapshot.Render(services);
    }

    /// <summary>
    /// Gets the rendered descriptors recorded for a tenant.
    /// </summary>
    /// <param name="tenantName">The tenant.</param>
    /// <returns>The rendered descriptors, or <see langword="null"/> when nothing was captured.</returns>
    public static string Find(string tenantName)
        => _snapshotsByTenant.TryGetValue(tenantName, out var snapshot) ? snapshot : null;
}
