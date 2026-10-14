using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Provisioning;

/// <summary>
/// Places every child tenant in the database of a pool with its own random table prefix. It works with every
/// database provider. Orchard Core drops the tenant tables when a suspended tenant is removed.
/// </summary>
public sealed class TablePrefixChildDatabaseProvisioner : IChildTenantDatabaseProvisioner
{
    /// <inheritdoc/>
    public bool CanProvision(ChildDatabaseProvisioningContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Strategy == ChildDatabaseStrategy.TablePrefixPerChild &&
            !string.IsNullOrEmpty(context.Pool?.DatabaseProvider) &&
            !string.IsNullOrEmpty(context.Pool.ConnectionString);
    }

    /// <inheritdoc/>
    public Task<ChildDatabaseProvisioningResult> ProvisionAsync(ChildDatabaseProvisioningContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var prefix = ProvisioningNames.CreateTablePrefix();

        return Task.FromResult(new ChildDatabaseProvisioningResult
        {
            DatabaseProvider = context.Pool.DatabaseProvider,
            ConnectionString = context.Pool.ConnectionString,
            TablePrefix = prefix,
            ProvisionedResource = prefix,
        });
    }

    /// <inheritdoc/>
    public Task DeprovisionAsync(ChildDatabaseProvisioningContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
