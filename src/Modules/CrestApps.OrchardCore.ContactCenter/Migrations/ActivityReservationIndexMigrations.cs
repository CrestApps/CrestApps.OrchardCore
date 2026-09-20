using CrestApps.Core.Data.YesSql.ContactCenter.Indexes;
using CrestApps.Core.Data.YesSql.ContactCenter.Migrations;
using OrchardCore.Data.Migration;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="ActivityReservationIndex"/>.
/// </summary>
internal sealed class ActivityReservationIndexMigrations : DataMigration
{
    private readonly ActivityReservationIndexMigrationsSchemaMigration _step;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityReservationIndexMigrations"/> class.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    public ActivityReservationIndexMigrations(
        IStore store,
        TimeProvider timeProvider)
    {
        _step = new ActivityReservationIndexMigrationsSchemaMigration(store, timeProvider);
    }

    /// <summary>
    /// Creates the reservation index table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> CreateAsync()
        => _step.CreateAsync(SchemaBuilder);

    /// <summary>
    /// Adds portable unique active-claim constraints to existing reservation indexes.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> UpdateFrom1Async()
        => _step.UpdateFromAsync(1, SchemaBuilder);

    /// <summary>
    /// Adds the time a reservation reached a terminal status, which is the age settled reservations are purged
    /// by. Neither the creation time nor the expiry can serve: an accepted reservation lives for as long as the
    /// work does, and it keeps an expiry in the future that never arrives.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> UpdateFrom2Async()
        => _step.UpdateFromAsync(2, SchemaBuilder);
}
