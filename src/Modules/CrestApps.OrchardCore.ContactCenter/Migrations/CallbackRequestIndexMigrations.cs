using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Migrations;
using OrchardCore.Data.Migration;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="CallbackRequestIndex"/>.
/// </summary>
internal sealed class CallbackRequestIndexMigrations : DataMigration
{
    private readonly CallbackRequestIndexMigrationsSchemaMigration _step;

    /// <summary>
    /// Initializes a new instance of the <see cref="CallbackRequestIndexMigrations"/> class.
    /// </summary>
    /// <param name="store">The document store, used to resolve the physical table name.</param>
    /// <param name="timeProvider">The time provider used to date the retention backfill.</param>
    public CallbackRequestIndexMigrations(
        IStore store,
        TimeProvider timeProvider)
    {
        _step = new CallbackRequestIndexMigrationsSchemaMigration(store, timeProvider);
    }

    /// <summary>
    /// Creates the callback request index table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> CreateAsync()
        => _step.CreateAsync(SchemaBuilder);

    /// <summary>
    /// Adds the promotion lease column to existing callback request index tables.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> UpdateFrom1Async()
        => _step.UpdateFromAsync(1, SchemaBuilder);

    /// <summary>
    /// Adds the last-modified time settled callbacks are purged by. The scheduled time cannot serve: a callback
    /// booked weeks ahead and then canceled keeps a future scheduled time, so it would never look old enough.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> UpdateFrom2Async()
        => _step.UpdateFromAsync(2, SchemaBuilder);
}
