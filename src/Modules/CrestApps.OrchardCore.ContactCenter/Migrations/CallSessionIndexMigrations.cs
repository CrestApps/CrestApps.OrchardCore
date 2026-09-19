using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Migrations;
using CrestApps.Core.Telephony.Services;
using OrchardCore.Data.Migration;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="CallSessionIndex"/> and enforces one call session per canonical
/// provider-call identity.
/// </summary>
internal sealed class CallSessionIndexMigrations : DataMigration
{
    private readonly CallSessionIndexMigrationsSchemaMigration _step;

    /// <summary>
    /// Initializes a new instance of the <see cref="CallSessionIndexMigrations"/> class.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    /// <param name="providerIdentityResolver">The resolver used to canonicalize legacy provider aliases before duplicate preflight and unique-index creation.</param>
    public CallSessionIndexMigrations(
        IStore store,
        IProviderIdentityResolver providerIdentityResolver)
    {
        _step = new CallSessionIndexMigrationsSchemaMigration(store, providerIdentityResolver);
    }

    /// <summary>
    /// Creates the call session index table and its supporting indexes.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> CreateAsync()
        => _step.CreateAsync(SchemaBuilder);

    /// <summary>
    /// Adds the portable provider-call claim column and unique constraint to existing call session indexes.
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

    /// <summary>
    /// Widens the provider-call identifier and the claim key composed from it so an external switch's long call
    /// identifier is stored and matched in full on the engines that enforce a declared column length.
    /// </summary>
    /// <remarks>
    /// The provider call identifier arrives verbatim from an external switch and can be long, but the column
    /// declared it at a length too short for it. An engine that enforces a declared length rejects an over-length
    /// write outright (PostgreSQL <c>22001</c>, SQL Server <c>8152</c>, MySQL <c>1406</c> under the default strict
    /// mode), so the session is never persisted; only non-strict MySQL truncates, and a truncated identifier
    /// stops a reconciliation lookup from finding its own session and — once the claim key composed from it
    /// outgrows its own column — forges a collision between two distinct calls, the opposite of what the unique
    /// claim exists to guarantee. Widening is forward-only and repairs no existing row. SQLite stores every text
    /// column as unbounded <c>TEXT</c>, so it never rejected or truncated and this rebuild is a value-preserving
    /// no-op there; the widening exists for the engines that enforce the length. The unique claim index and the
    /// covering index that both name a rebuilt column come down before the rebuild — SQLite refuses to drop a
    /// column an index refers to — and are recreated at the wider columns afterwards.
    /// </remarks>
    /// <returns>The migration version number.</returns>
    public Task<int> UpdateFrom3Async()
        => _step.UpdateFromAsync(3, SchemaBuilder);
}
