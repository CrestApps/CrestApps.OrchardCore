using CrestApps.Core.Data.YesSql.Omnichannel.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Migrations;
using OrchardCore.Data.Migration;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Migrations;

/// <summary>
/// Creates and evolves the schema for the omnichannel activity index.
/// </summary>
/// <remarks>
/// The schema itself lives in <see cref="OmnichannelActivityIndexMigrationsSchemaMigration"/>; this class only
/// hands Orchard's schema builder to it. The type name is part of the stored data, because Orchard records the
/// applied version under this class's full name, so it must not be renamed.
/// </remarks>
internal sealed class OmnichannelActivityIndexMigrations : DataMigration
{
    private readonly OmnichannelActivityIndexMigrationsSchemaMigration _step;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelActivityIndexMigrations"/> class.
    /// </summary>
    /// <param name="store">The YesSql store, used to resolve the schema the index table lives in.</param>
    public OmnichannelActivityIndexMigrations(IStore store)
    {
        _step = new OmnichannelActivityIndexMigrationsSchemaMigration(store);
    }

    /// <summary>
    /// Creates the omnichannel activity index table with the final set of columns and indexes.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> CreateAsync()
        => _step.CreateAsync(SchemaBuilder);

    /// <summary>
    /// Adds Contact Center assignment and classification columns to the activity index.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> UpdateFrom1Async()
        => _step.UpdateFromAsync(1, SchemaBuilder);

    /// <summary>
    /// Skips the superseded username-index migration.
    /// </summary>
    /// <remarks>
    /// This step is static, so it cannot reach the instance that holds the schema migration. The same jump is
    /// recorded as the step from version 2 in
    /// <see cref="OmnichannelActivityIndexMigrationsSchemaMigration.UpdateFromAsync"/>, and the two must stay
    /// in agreement.
    /// </remarks>
    /// <returns>The migration version number.</returns>
    public static int UpdateFrom2()
    {
        return 4;
    }

    /// <summary>
    /// Removes usernames from the activity index because user presentation is resolved by shapes.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> UpdateFrom3Async()
        => _step.UpdateFromAsync(3, SchemaBuilder);

    /// <summary>
    /// Declares the enum-valued columns as the integer columns they have always held, and restores the
    /// assignment column to the assigned-activity index, so an upgraded tenant matches a freshly created one.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> UpdateFrom4Async()
        => _step.UpdateFromAsync(4, SchemaBuilder);

    /// <summary>
    /// Adds the AI-escalation flag used by containment reporting to count handoffs across channels.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> UpdateFrom5Async()
        => _step.UpdateFromAsync(5, SchemaBuilder);
}
