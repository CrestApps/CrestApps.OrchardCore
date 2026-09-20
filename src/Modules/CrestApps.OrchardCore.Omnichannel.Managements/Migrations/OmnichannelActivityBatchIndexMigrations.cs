using CrestApps.Core.Data.YesSql.Omnichannel.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Migrations;
using Microsoft.Extensions.Logging;
using OrchardCore.Data;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Migrations;

/// <summary>
/// Creates and repairs the schema for the omnichannel activity batch index table.
/// </summary>
/// <remarks>
/// The schema itself lives in <see cref="OmnichannelActivityBatchIndexMigrationsSchemaMigration"/>; this class
/// only hands Orchard's schema builder to it. The type name is part of the stored data, because Orchard records
/// the applied version under this class's full name, so it must not be renamed.
/// </remarks>
internal sealed class OmnichannelActivityBatchIndexMigrations : OmnichannelIndexMigration
{
    private readonly OmnichannelActivityBatchIndexMigrationsSchemaMigration _step;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelActivityBatchIndexMigrations"/> class.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    /// <param name="dbConnectionAccessor">The database connection accessor.</param>
    /// <param name="logger">The logger.</param>
    public OmnichannelActivityBatchIndexMigrations(
        IStore store,
        IDbConnectionAccessor dbConnectionAccessor,
        ILogger<OmnichannelActivityBatchIndexMigrations> logger)
        : base(store, dbConnectionAccessor, logger)
    {
        _step = new OmnichannelActivityBatchIndexMigrationsSchemaMigration(store, dbConnectionAccessor, logger);
    }

    /// <summary>
    /// Creates the omnichannel activity batch index table with the final set of columns and indexes.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> CreateAsync()
        => _step.CreateAsync(SchemaBuilder);

    /// <summary>
    /// Adds the activity batch source column in an isolated transaction so it survives sibling migration failures.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> UpdateFrom1Async()
        => _step.UpdateFromAsync(1, SchemaBuilder);

    /// <summary>
    /// Adds the activity batch created UTC column, used to order batches by newest first, in an isolated
    /// transaction so it survives sibling migration failures in the same feature.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> UpdateFrom2Async()
        => _step.UpdateFromAsync(2, SchemaBuilder);

    /// <summary>
    /// Repairs databases whose migration version was recorded at or above the step that added the
    /// <c>Source</c> and <c>CreatedUtc</c> columns while the physical columns were rolled back with a failed
    /// sibling migration. Because the earlier version-gated steps no longer run on those databases, this
    /// step verifies each column and adds only the ones that are missing, so the activity batches screen can
    /// order by <c>CreatedUtc</c> again.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> UpdateFrom3Async()
        => _step.UpdateFromAsync(3, SchemaBuilder);
}
