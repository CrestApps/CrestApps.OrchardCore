using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Migrations;
using OrchardCore.Data.Migration;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="ContactCenterProcessedEventIndex"/> and enforces per-handler
/// event idempotency through a composite unique constraint.
/// </summary>
internal sealed class ContactCenterProcessedEventIndexMigrations : DataMigration
{
    private readonly ContactCenterProcessedEventIndexMigrationsSchemaMigration _step;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterProcessedEventIndexMigrations"/> class.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    public ContactCenterProcessedEventIndexMigrations(
        IStore store,
        TimeProvider timeProvider)
    {
        _step = new ContactCenterProcessedEventIndexMigrationsSchemaMigration(store, timeProvider);
    }

    /// <summary>
    /// Creates the processed-event index table and its per-handler event uniqueness constraint.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> CreateAsync()
        => _step.CreateAsync(SchemaBuilder);

    /// <summary>
    /// Adds the processed time these markers are purged by, and gives rows that predate the column one full
    /// retention window rather than the default instant, which every cutoff is newer than.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> UpdateFrom1Async()
        => _step.UpdateFromAsync(1, SchemaBuilder);
}
