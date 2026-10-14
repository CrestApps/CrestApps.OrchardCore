using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Provisioning;

/// <summary>
/// Picks the <see cref="IChildTenantDatabaseProvisioner"/> for a strategy and a pool configured on the host, and runs it.
/// </summary>
public sealed class ChildDatabaseProvisioning
{
    private readonly IEnumerable<IChildTenantDatabaseProvisioner> _provisioners;
    private readonly TenantHierarchyOptions _options;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChildDatabaseProvisioning"/> class.
    /// </summary>
    /// <param name="provisioners">The database provisioners.</param>
    /// <param name="options">The tenant hierarchy options.</param>
    /// <param name="logger">The logger.</param>
    public ChildDatabaseProvisioning(
        IEnumerable<IChildTenantDatabaseProvisioner> provisioners,
        IOptions<TenantHierarchyOptions> options,
        ILogger<ChildDatabaseProvisioning> logger)
    {
        _provisioners = provisioners;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Returns whether a strategy and a pool can be used: a provisioner handles them and the pool is configured.
    /// </summary>
    /// <param name="strategy">The strategy.</param>
    /// <param name="poolName">The pool name.</param>
    public bool CanProvision(ChildDatabaseStrategy strategy, string poolName)
    {
        var context = CreateContext("probe", strategy, poolName, null);

        return context is not null && _provisioners.Any(provisioner => provisioner.CanProvision(context));
    }

    /// <summary>
    /// Creates the database of a child tenant. Returns a failed result when no provisioner handles the strategy and pool.
    /// </summary>
    /// <param name="tenantName">The opaque tenant name.</param>
    /// <param name="strategy">The strategy.</param>
    /// <param name="poolName">The pool name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<ChildDatabaseProvisioningResult> ProvisionAsync(
        string tenantName,
        ChildDatabaseStrategy strategy,
        string poolName,
        CancellationToken cancellationToken = default)
    {
        var context = CreateContext(tenantName, strategy, poolName, null);
        var provisioner = context is null
            ? null
            : _provisioners.FirstOrDefault(candidate => candidate.CanProvision(context));

        if (provisioner is null)
        {
            _logger.LogError("No database provisioner handles strategy '{Strategy}' with pool '{Pool}'.", strategy, poolName);

            return ChildDatabaseProvisioningResult.Failed("No database provisioner handles the strategy and pool of the parent policy.");
        }

        try
        {
            return await provisioner.ProvisionAsync(context, cancellationToken);
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            _logger.LogError(ex, "The database of child tenant '{ChildTenant}' could not be provisioned.", tenantName);

            return ChildDatabaseProvisioningResult.Failed("The database could not be provisioned.");
        }
    }

    /// <summary>
    /// Removes the database of a removed child tenant. Failures are logged, not thrown, because the tenant is already gone.
    /// </summary>
    /// <param name="tenantName">The opaque tenant name.</param>
    /// <param name="strategy">The strategy.</param>
    /// <param name="poolName">The pool name.</param>
    /// <param name="provisionedResource">The database, schema or table prefix that was created.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task DeprovisionAsync(
        string tenantName,
        ChildDatabaseStrategy strategy,
        string poolName,
        string provisionedResource,
        CancellationToken cancellationToken = default)
    {
        if (strategy == ChildDatabaseStrategy.SqlitePerChild || string.IsNullOrEmpty(provisionedResource))
        {
            return;
        }

        var context = CreateContext(tenantName, strategy, poolName, provisionedResource);
        var provisioner = context is null
            ? null
            : _provisioners.FirstOrDefault(candidate => candidate.CanProvision(context));

        if (provisioner is null)
        {
            _logger.LogWarning("No database provisioner removes '{Resource}' of child tenant '{ChildTenant}'.", provisionedResource, tenantName);

            return;
        }

        try
        {
            await provisioner.DeprovisionAsync(context, cancellationToken);
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            _logger.LogError(ex, "The database '{Resource}' of removed child tenant '{ChildTenant}' could not be removed.", provisionedResource, tenantName);
        }
    }

    private ChildDatabaseProvisioningContext CreateContext(string tenantName, ChildDatabaseStrategy strategy, string poolName, string provisionedResource)
    {
        DatabasePoolOptions pool = null;

        if (strategy != ChildDatabaseStrategy.SqlitePerChild &&
            (string.IsNullOrEmpty(poolName) || !_options.DatabasePools.TryGetValue(poolName, out pool)))
        {
            return null;
        }

        return new ChildDatabaseProvisioningContext
        {
            TenantName = tenantName,
            Strategy = strategy,
            PoolName = poolName,
            Pool = pool,
            ProvisionedResource = provisionedResource,
        };
    }
}
