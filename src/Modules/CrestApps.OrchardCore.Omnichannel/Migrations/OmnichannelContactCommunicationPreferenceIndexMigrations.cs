using CrestApps.OrchardCore.Omnichannel.Core.Migrations;
using Microsoft.Extensions.Logging;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Migrations;

/// <summary>
/// Creates the schema for the contact communication preference index.
/// </summary>
/// <remarks>
/// The schema itself lives in
/// <see cref="OmnichannelContactCommunicationPreferenceIndexMigrationsSchemaMigration"/>; this class only hands
/// Orchard's schema builder to it. The type name is part of the stored data, because Orchard records the
/// applied version under this class's full name, so it must not be renamed.
/// </remarks>
internal sealed class OmnichannelContactCommunicationPreferenceIndexMigrations : DataMigration
{
    private readonly OmnichannelContactCommunicationPreferenceIndexMigrationsSchemaMigration _step;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelContactCommunicationPreferenceIndexMigrations"/> class.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    /// <param name="dbConnectionAccessor">The database connection accessor.</param>
    /// <param name="logger">The logger.</param>
    public OmnichannelContactCommunicationPreferenceIndexMigrations(
        IStore store,
        IDbConnectionAccessor dbConnectionAccessor,
        ILogger<OmnichannelContactCommunicationPreferenceIndexMigrations> logger)
    {
        _step = new OmnichannelContactCommunicationPreferenceIndexMigrationsSchemaMigration(store, dbConnectionAccessor, logger);
    }

    /// <summary>
    /// Creates a new async.
    /// </summary>
    public Task<int> CreateAsync()
        => _step.CreateAsync(SchemaBuilder);

    /// <summary>
    /// Updates the from1 async.
    /// </summary>
    public Task<int> UpdateFrom1Async()
        => _step.UpdateFromAsync(1, SchemaBuilder);

    /// <summary>
    /// Removes the chat preference columns, which no channel could ever act on.
    /// </summary>
    public Task<int> UpdateFrom2Async()
        => _step.UpdateFromAsync(2, SchemaBuilder);
}
