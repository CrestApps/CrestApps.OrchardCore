using Microsoft.Extensions.Logging;
using YesSql;
using YesSql.Sql;

namespace CrestApps.Core.Data.YesSql.Migrations;

/// <summary>
/// Brings a database up to date by running the schema migrations it has not yet had applied.
/// </summary>
/// <remarks>
/// For a host with no migration mechanism of its own. A host that has one - Orchard Core, for
/// instance - drives the same <see cref="ISchemaMigration"/> steps from it instead, with the same
/// version numbers, and never uses this.
/// </remarks>
public sealed class SchemaMigrationRunner
{
    private readonly IStore _store;
    private readonly IEnumerable<ISchemaMigration> _migrations;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SchemaMigrationRunner"/> class.
    /// </summary>
    /// <param name="store">The store whose schema is migrated.</param>
    /// <param name="migrations">The migrations to consider.</param>
    /// <param name="logger">The logger.</param>
    public SchemaMigrationRunner(IStore store, IEnumerable<ISchemaMigration> migrations, ILogger<SchemaMigrationRunner> logger)
    {
        _store = store;
        _migrations = migrations;
        _logger = logger;
    }

    /// <summary>
    /// Runs every migration that is behind, and records how far each one got.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of migrations that advanced.</returns>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        await using var session = _store.CreateSession();

        var version = await session.Query<SchemaVersion>().FirstOrDefaultAsync(cancellationToken)
            ?? new SchemaVersion();

        var builder = new SchemaBuilder(_store.Configuration, await session.BeginTransactionAsync(cancellationToken));
        var advanced = 0;

        foreach (var migration in _migrations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var applied = version.AppliedVersions.TryGetValue(migration.Name, out var current) ? current : 0;
            var target = applied;

            try
            {
                if (applied == 0)
                {
                    target = await migration.CreateAsync(builder);
                }
                else
                {
                    // Steps run one at a time, each reporting where it left the schema, so a
                    // migration that has several versions to catch up on applies them in order.
                    int next;

                    while ((next = await migration.UpdateFromAsync(target, builder)) > target)
                    {
                        target = next;
                    }
                }
            }
            catch (Exception ex)
            {
                // Recorded and rethrown: a half-applied schema that is reported as applied is worse
                // than a failure, because the next run skips the step that did not finish.
                _logger.LogError(
                    ex,
                    "Schema migration '{Migration}' failed at version {Version}.",
                    migration.Name,
                    target);

                throw;
            }

            if (target != applied)
            {
                version.AppliedVersions[migration.Name] = target;
                advanced++;
            }
        }

        if (advanced > 0)
        {
            await session.SaveAsync(version, cancellationToken: cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }

        return advanced;
    }
}
