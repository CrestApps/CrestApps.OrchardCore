using CrestApps.Core.Data.YesSql.Migrations;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Migrations;

/// <summary>
/// Creates the schema for the <see cref="ActivityQueueIndex"/>.
/// </summary>
internal sealed class ActivityQueueIndexMigrationsSchemaMigration : ISchemaMigration
{
    /// <inheritdoc/>
    /// <remarks>
    /// The stored name is the Orchard migration class's own name, so a database migrated under either
    /// host agrees on which version has already been applied.
    /// </remarks>
    public string Name => "ActivityQueueIndexMigrations";

    /// <summary>
    /// Creates the queue index table.
    /// </summary>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync(ISchemaBuilder builder)
    {
        await builder.CreateMapIndexTableAsync<ActivityQueueIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.WithLength(255))
            .Column<string>("QueueGroupId", column => column.WithLength(26))
            .Column<bool>("Enabled"),
            collection: ContactCenterStorage.CollectionName
        );

        await builder.AlterIndexTableAsync<ActivityQueueIndex>(table => table
            .CreateIndex("IDX_ActivityQueueIndex_DocumentId", "DocumentId", "ItemId", "Enabled"),
            collection: ContactCenterStorage.CollectionName
        );

        return 2;
    }

    /// <summary>
    /// Moves the schema forward one step from an already-applied version.
    /// </summary>
    /// <param name="version">The version currently applied.</param>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The version the schema is now at, or the same version when there is no step from it.</returns>
    public async Task<int> UpdateFromAsync(int version, ISchemaBuilder builder)
    {
        switch (version)
        {
            case 1:
                return await UpdateFrom1Async(builder);

            default:
                return version;
        }
    }

    /// <summary>
    /// Moves the schema from version 1 to the next version.
    /// </summary>
    /// <remarks>
    /// Kept as its own method, matching the name the Orchard migration used, because the additive-only
    /// guard authorizes destructive steps per method: folding every version into one switch would make
    /// one authorization cover them all.
    /// </remarks>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The version the schema is now at.</returns>
    private static async Task<int> UpdateFrom1Async(ISchemaBuilder builder)
    {
                    // Adds the optional queue-group identifier used by catalog organization and reporting.
                    await builder.AlterIndexTableAsync<ActivityQueueIndex>(table =>
                    {
                        table.AddColumn<string>("QueueGroupId", column => column.WithLength(26));
                    },
                        collection: ContactCenterStorage.CollectionName
                    );

                    return 2;
                }
}
