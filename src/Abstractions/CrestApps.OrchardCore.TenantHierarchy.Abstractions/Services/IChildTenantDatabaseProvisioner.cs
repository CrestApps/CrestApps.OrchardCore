using CrestApps.OrchardCore.TenantHierarchy.Models;

namespace CrestApps.OrchardCore.TenantHierarchy.Services;

/// <summary>
/// Creates and removes the database of a child tenant for one <see cref="ChildDatabaseStrategy"/>. Orchard Core never
/// creates or drops databases, schemas or logins, so the tenant hierarchy does it through provisioners. Register an
/// implementation at the host level to add a database server.
/// </summary>
public interface IChildTenantDatabaseProvisioner
{
    /// <summary>
    /// Returns whether this provisioner handles the strategy and pool of the context.
    /// </summary>
    /// <param name="context">The provisioning context.</param>
    bool CanProvision(ChildDatabaseProvisioningContext context);

    /// <summary>
    /// Creates the database, schema or table prefix of a child tenant and returns the settings the tenant uses.
    /// </summary>
    /// <param name="context">The provisioning context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<ChildDatabaseProvisioningResult> ProvisionAsync(ChildDatabaseProvisioningContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes what <see cref="ProvisionAsync"/> created. Orchard Core has already dropped the tenant tables. A pool
    /// that retains removed databases renames them instead of dropping them.
    /// </summary>
    /// <param name="context">The provisioning context, with <see cref="ChildDatabaseProvisioningContext.ProvisionedResource"/> set.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task DeprovisionAsync(ChildDatabaseProvisioningContext context, CancellationToken cancellationToken = default);
}
