using CrestApps.Core.Data.YesSql.ContactCenter.Indexes;
using CrestApps.Core.Data.YesSql.ContactCenter.Migrations;
using OrchardCore.Data.Migration;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="QueueItemIndex"/>.
/// </summary>
internal sealed class QueueItemIndexMigrations : DataMigration
{
    private readonly QueueItemIndexMigrationsSchemaMigration _step;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueueItemIndexMigrations"/> class.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    public QueueItemIndexMigrations(
        IStore store,
        TimeProvider timeProvider)
    {
        _step = new QueueItemIndexMigrationsSchemaMigration(store, timeProvider);
    }

    /// <summary>
    /// Creates the queue item index table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> CreateAsync()
        => _step.CreateAsync(SchemaBuilder);

    /// <summary>
    /// Adds a portable unique active-queue-item constraint to existing indexes.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> UpdateFrom1Async()
        => _step.UpdateFromAsync(1, SchemaBuilder);

    /// <summary>
    /// Adds the time an item left the queue, which is what settled items are purged by. Purging by arrival
    /// time instead would delete an item the moment it was handled if it had waited longer than the window.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> UpdateFrom2Async()
        => _step.UpdateFromAsync(2, SchemaBuilder);

    /// <summary>
    /// Adds the predicate-led index the agent workspace reads. Every poll asks how many items are waiting in
    /// each queue the agent belongs to, and no existing index answers that: the composite leads with
    /// <c>DocumentId</c>, which serves join-back and delete-by-document but says nothing about a queue, and the
    /// retention index leads with <c>Status</c>, so the planner falls back to seeking that and walking every
    /// waiting item in the tenant to find the ones belonging to the queue being asked about — once per queue, on
    /// every poll of every signed-in agent.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> UpdateFrom3Async()
        => _step.UpdateFromAsync(3, SchemaBuilder);
}
