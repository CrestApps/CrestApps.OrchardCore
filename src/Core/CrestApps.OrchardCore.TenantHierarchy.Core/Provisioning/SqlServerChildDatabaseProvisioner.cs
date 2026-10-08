using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.Data.SqlClient;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Provisioning;

/// <summary>
/// Places child tenants on a SQL Server pool, with a database and login per child or a schema and login per child.
/// The login of a child can reach only its own database or schema.
/// </summary>
public sealed class SqlServerChildDatabaseProvisioner : IChildTenantDatabaseProvisioner
{
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlServerChildDatabaseProvisioner"/> class.
    /// </summary>
    /// <param name="clock">The clock.</param>
    public SqlServerChildDatabaseProvisioner(IClock clock)
    {
        _clock = clock;
    }

    /// <inheritdoc/>
    public bool CanProvision(ChildDatabaseProvisioningContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return (context.Strategy == ChildDatabaseStrategy.DatabasePerChild || context.Strategy == ChildDatabaseStrategy.SchemaPerChild) &&
            string.Equals(context.Pool?.DatabaseProvider, DatabaseProviderNames.SqlServer, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrEmpty(context.Pool.ConnectionString);
    }

    /// <inheritdoc/>
    public async Task<ChildDatabaseProvisioningResult> ProvisionAsync(ChildDatabaseProvisioningContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var name = ProvisioningNames.ForTenant(context.TenantName);
        var password = ProvisioningNames.CreatePassword();
        var builder = new SqlConnectionStringBuilder(context.Pool.ConnectionString);

        if (context.Strategy == ChildDatabaseStrategy.DatabasePerChild)
        {
            await ExecuteAsync(context.Pool.ConnectionString, SqlServerProvisioningScripts.CreateDatabase(name, name, password), cancellationToken);

            var adminInDatabase = new SqlConnectionStringBuilder(context.Pool.ConnectionString)
            {
                InitialCatalog = name,
            };

            await ExecuteAsync(adminInDatabase.ConnectionString, SqlServerProvisioningScripts.GrantDatabaseOwner(name), cancellationToken);

            builder.InitialCatalog = name;
            builder.IntegratedSecurity = false;
            builder.UserID = name;
            builder.Password = password;

            return new ChildDatabaseProvisioningResult
            {
                DatabaseProvider = DatabaseProviderNames.SqlServer,
                ConnectionString = builder.ConnectionString,
                TablePrefix = string.Empty,
                ProvisionedResource = name,
            };
        }

        await ExecuteAsync(context.Pool.ConnectionString, SqlServerProvisioningScripts.CreateSchema(name, name, password), cancellationToken);

        builder.IntegratedSecurity = false;
        builder.UserID = name;
        builder.Password = password;

        return new ChildDatabaseProvisioningResult
        {
            DatabaseProvider = DatabaseProviderNames.SqlServer,
            ConnectionString = builder.ConnectionString,
            Schema = name,
            TablePrefix = string.Empty,
            ProvisionedResource = name,
        };
    }

    /// <inheritdoc/>
    public Task DeprovisionAsync(ChildDatabaseProvisioningContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var name = ProvisioningNames.EnsureSafeIdentifier(context.ProvisionedResource);

        if (context.Strategy == ChildDatabaseStrategy.DatabasePerChild)
        {
            var retained = context.Pool.RetainRemovedDatabases
                ? ProvisioningNames.ForRetainedDatabase(name, _clock.UtcNow)
                : null;

            return ExecuteAsync(context.Pool.ConnectionString, SqlServerProvisioningScripts.RemoveDatabase(name, name, retained), cancellationToken);
        }

        return ExecuteAsync(context.Pool.ConnectionString, SqlServerProvisioningScripts.RemoveSchema(name, name), cancellationToken);
    }

    private static async Task ExecuteAsync(string connectionString, IEnumerable<string> statements, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var statement in statements)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = statement;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
