using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using Npgsql;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Provisioning;

/// <summary>
/// Places child tenants on a PostgreSQL pool, with a database and role per child or a schema and role per child.
/// The role of a child can reach only its own database or schema.
/// </summary>
public sealed class PostgreSqlChildDatabaseProvisioner : IChildTenantDatabaseProvisioner
{
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgreSqlChildDatabaseProvisioner"/> class.
    /// </summary>
    /// <param name="clock">The clock.</param>
    public PostgreSqlChildDatabaseProvisioner(IClock clock)
    {
        _clock = clock;
    }

    /// <inheritdoc/>
    public bool CanProvision(ChildDatabaseProvisioningContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return (context.Strategy == ChildDatabaseStrategy.DatabasePerChild || context.Strategy == ChildDatabaseStrategy.SchemaPerChild) &&
            string.Equals(context.Pool?.DatabaseProvider, DatabaseProviderNames.PostgreSql, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrEmpty(context.Pool.ConnectionString);
    }

    /// <inheritdoc/>
    public async Task<ChildDatabaseProvisioningResult> ProvisionAsync(ChildDatabaseProvisioningContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var name = ProvisioningNames.ForTenant(context.TenantName);
        var password = ProvisioningNames.CreatePassword();
        var builder = new NpgsqlConnectionStringBuilder(context.Pool.ConnectionString);

        if (context.Strategy == ChildDatabaseStrategy.DatabasePerChild)
        {
            await ExecuteAsync(context.Pool.ConnectionString, PostgreSqlProvisioningScripts.CreateDatabase(name, name, password), cancellationToken);

            builder.Database = name;
            builder.Username = name;
            builder.Password = password;

            return new ChildDatabaseProvisioningResult
            {
                DatabaseProvider = DatabaseProviderNames.PostgreSql,
                ConnectionString = builder.ConnectionString,
                TablePrefix = string.Empty,
                ProvisionedResource = name,
            };
        }

        var database = await ExecuteAsync(context.Pool.ConnectionString, PostgreSqlProvisioningScripts.CreateSchema(name, name, password), cancellationToken);

        // The schema lives in the database the pool connected to. Name it, because without a database Npgsql would
        // connect the child to a database named after its own login.
        builder.Database = database;
        builder.Username = name;
        builder.Password = password;

        return new ChildDatabaseProvisioningResult
        {
            DatabaseProvider = DatabaseProviderNames.PostgreSql,
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

            NpgsqlConnection.ClearAllPools();

            return ExecuteAsync(context.Pool.ConnectionString, PostgreSqlProvisioningScripts.RemoveDatabase(name, name, retained), cancellationToken);
        }

        return ExecuteAsync(context.Pool.ConnectionString, PostgreSqlProvisioningScripts.RemoveSchema(name, name), cancellationToken);
    }

    private static async Task<string> ExecuteAsync(string connectionString, IEnumerable<string> statements, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var statement in statements)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = statement;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        return connection.Database;
    }
}
