using CrestApps.Core.Data.YesSql.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Migrations;
using OrchardCore.Data.Migration;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="ProviderCommandIndex"/>, including the unique idempotency key that
/// guarantees one provider command per key per tenant.
/// </summary>
internal sealed class ProviderCommandIndexMigrations : DataMigration
{
    private readonly ProviderCommandIndexMigrationsSchemaMigration _step;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderCommandIndexMigrations"/> class.
    /// </summary>
    /// <param name="store">The document store, used to resolve the physical table name.</param>
    /// <param name="timeProvider">The time provider used to date the retention backfill.</param>
    public ProviderCommandIndexMigrations(
        IStore store,
        TimeProvider timeProvider)
    {
        _step = new ProviderCommandIndexMigrationsSchemaMigration(store, timeProvider);
    }

    /// <summary>
    /// Creates the provider command index table and its idempotency, due, and reclaim indexes.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> CreateAsync()
        => _step.CreateAsync(SchemaBuilder);

    /// <summary>
    /// Adds the completion time settled commands are purged by. Neither the retry time nor the lease time can
    /// serve, because neither advances once a command has finished.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> UpdateFrom1Async()
        => _step.UpdateFromAsync(1, SchemaBuilder);
}
