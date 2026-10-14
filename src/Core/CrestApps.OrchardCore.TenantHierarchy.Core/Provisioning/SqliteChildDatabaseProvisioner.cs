using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Provisioning;

/// <summary>
/// Places every child tenant in its own SQLite database file in the tenant's own folder. Orchard Core deletes the
/// folder, and the file with it, when the tenant is removed.
/// </summary>
public sealed class SqliteChildDatabaseProvisioner : IChildTenantDatabaseProvisioner
{
    /// <summary>
    /// The name of the database file in the tenant folder.
    /// </summary>
    public const string DatabaseFileName = "OrchardCore.db";

    /// <inheritdoc/>
    public bool CanProvision(ChildDatabaseProvisioningContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Strategy == ChildDatabaseStrategy.SqlitePerChild;
    }

    /// <inheritdoc/>
    public Task<ChildDatabaseProvisioningResult> ProvisionAsync(ChildDatabaseProvisioningContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        return Task.FromResult(new ChildDatabaseProvisioningResult
        {
            DatabaseProvider = DatabaseProviderNames.Sqlite,
            DatabaseName = DatabaseFileName,
            TablePrefix = string.Empty,
        });
    }

    /// <inheritdoc/>
    public Task DeprovisionAsync(ChildDatabaseProvisioningContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
