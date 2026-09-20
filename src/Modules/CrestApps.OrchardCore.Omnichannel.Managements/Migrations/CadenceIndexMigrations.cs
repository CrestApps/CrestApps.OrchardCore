using CrestApps.Core.Data.YesSql.Omnichannel.Migrations;
using CrestApps.Core.Data.YesSql.Omnichannel.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Migrations;
using Microsoft.Extensions.Logging;
using OrchardCore.Data;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Migrations;

/// <summary>
/// Creates the schema for the cadence index table.
/// </summary>
/// <remarks>
/// The schema itself lives in <see cref="CadenceIndexMigrationsSchemaMigration"/>; this class only hands
/// Orchard's schema builder to it. The type name is part of the stored data, because Orchard records the
/// applied version under this class's full name, so it must not be renamed.
/// </remarks>
internal sealed class CadenceIndexMigrations : OmnichannelIndexMigration
{
    private readonly CadenceIndexMigrationsSchemaMigration _step;

    /// <summary>
    /// Initializes a new instance of the <see cref="CadenceIndexMigrations"/> class.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    /// <param name="dbConnectionAccessor">The database connection accessor.</param>
    /// <param name="logger">The logger.</param>
    public CadenceIndexMigrations(
        IStore store,
        IDbConnectionAccessor dbConnectionAccessor,
        ILogger<CadenceIndexMigrations> logger)
        : base(store, dbConnectionAccessor, logger)
    {
        _step = new CadenceIndexMigrationsSchemaMigration();
    }

    /// <summary>
    /// Creates the cadence index table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> CreateAsync()
        => _step.CreateAsync(SchemaBuilder);
}
