using CrestApps.Core.Data.YesSql.ContactCenter.Indexes;
using CrestApps.Core.Data.YesSql.ContactCenter.Migrations;
using OrchardCore.Data.Migration;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="ContactCenterOutboxMessageIndex"/>.
/// </summary>
internal sealed class ContactCenterOutboxMessageIndexMigrations : DataMigration
{
    private readonly ContactCenterOutboxMessageIndexMigrationsSchemaMigration _step;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterOutboxMessageIndexMigrations"/> class.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    public ContactCenterOutboxMessageIndexMigrations(
        IStore store,
        TimeProvider timeProvider)
    {
        _step = new ContactCenterOutboxMessageIndexMigrationsSchemaMigration(store, timeProvider);
    }

    /// <summary>
    /// Creates the outbox message index table and its supporting index.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> CreateAsync()
        => _step.CreateAsync(SchemaBuilder);

    /// <summary>
    /// Adds the creation time settled messages are purged by. The retry time cannot serve: a settled message
    /// keeps whatever retry time it last held, so it is not an age.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> UpdateFrom1Async()
        => _step.UpdateFromAsync(1, SchemaBuilder);
}
