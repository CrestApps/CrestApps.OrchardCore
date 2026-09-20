using CrestApps.Core.Data.YesSql.ContactCenter.Indexes;
using CrestApps.Core.Data.YesSql.ContactCenter.Migrations;
using OrchardCore.Data.Migration;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="ContactCenterEventMetricIndex"/>.
/// </summary>
internal sealed class ContactCenterEventMetricIndexMigrations : DataMigration
{
    private readonly ContactCenterEventMetricIndexMigrationsSchemaMigration _step;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterEventMetricIndexMigrations"/> class.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    public ContactCenterEventMetricIndexMigrations(IStore store)
    {
        _step = new ContactCenterEventMetricIndexMigrationsSchemaMigration(store);
    }

    /// <summary>
    /// Creates the event metric index table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> CreateAsync()
        => _step.CreateAsync(SchemaBuilder);

    /// <summary>
    /// Adds the portable uniqueness constraint for an existing event-metric index.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> UpdateFrom1Async()
        => _step.UpdateFromAsync(1, SchemaBuilder);

    /// <summary>
    /// Adds the covering index the retention purge scans. Without it every terminating batch of the drain loop
    /// is a full scan of a table that grows with traffic, which is exactly the table size retention exists for.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> UpdateFrom2Async()
        => _step.UpdateFromAsync(2, SchemaBuilder);
}
