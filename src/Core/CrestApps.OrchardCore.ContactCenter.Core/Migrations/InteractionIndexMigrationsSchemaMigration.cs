using CrestApps.Core.Data.YesSql.Migrations;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Models;
using YesSql.Sql;
using CrestApps.Core.Telephony.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Migrations;

/// <summary>
/// Creates the schema for the <see cref="InteractionIndex"/>.
/// </summary>
internal sealed class InteractionIndexMigrationsSchemaMigration : ISchemaMigration
{
    /// <inheritdoc/>
    /// <remarks>
    /// The stored name is the Orchard migration class's own name, so a database migrated under either
    /// host agrees on which version has already been applied.
    /// </remarks>
    public string Name => "InteractionIndexMigrations";

    /// <summary>
    /// Creates the interaction index table and its supporting indexes.
    /// </summary>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync(ISchemaBuilder builder)
    {
        await builder.CreateMapIndexTableAsync<InteractionIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<InteractionChannel>("Channel")
            .Column<InteractionDirection>("Direction")
            .Column<InteractionStatus>("Status")
            .Column<string>("ActivityItemId", column => column.WithLength(26))
            .Column<string>("ProviderName", column => column.WithLength(ContactCenterStorage.ProviderNameLength))
            .Column<string>("ProviderInteractionId", column => column.WithLength(128))
            .Column<string>("ProviderLegId", column => column.WithLength(128))
            .Column<string>("QueueId", column => column.WithLength(26))
            .Column<string>("AgentId", column => column.WithLength(26))
            .Column<string>("CorrelationId", column => column.WithLength(26))
            .Column<DateTime>("CreatedUtc", column => column.NotNull())
            .Column<DateTime>("EndedUtc"),
            collection: ContactCenterStorage.CollectionName
        );

        await builder.AlterIndexTableAsync<InteractionIndex>(table => table
            .CreateIndex("IDX_InteractionIndex_DocumentId",
                "DocumentId",
                "ItemId",
                "Status",
                "QueueId",
                "AgentId"),
            collection: ContactCenterStorage.CollectionName
        );

        await builder.AlterIndexTableAsync<InteractionIndex>(table => table
            .CreateIndex("IDX_InteractionIndex_Lookup",
                "ActivityItemId",
                "ProviderInteractionId",
                "ProviderLegId",
                "CorrelationId"),
            collection: ContactCenterStorage.CollectionName
        );

        return 1;
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

            case 2:
                return await UpdateFrom2Async(builder);

            case 3:
                return await UpdateFrom3Async(builder);

            case 4:
                return await UpdateFrom4Async(builder);

            case 5:
                return await UpdateFrom5Async(builder);

            default:
                return version;
        }
    }

    /// <summary>
    /// Moves the schema from version 1 to the next version.
    /// </summary>
    /// <remarks>
    /// Kept as its own method, under the name the Orchard migration used, because the additive-only
    /// guard authorizes destructive steps per method. Folding every version into one switch would let
    /// a single authorization cover them all.
    /// </remarks>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The version the schema is now at.</returns>
    private static async Task<int> UpdateFrom1Async(ISchemaBuilder builder)
    {
                    // Adds after-call wrap-up timestamps used by handle-time reporting.
                    await builder.AlterIndexTableAsync<InteractionIndex>(table =>
                    {
                        table.AddColumn<DateTime>("WrapUpStartedUtc");
                        table.AddColumn<DateTime>("WrapUpCompletedUtc");
                    },
                        collection: ContactCenterStorage.CollectionName
                    );

                    return 2;
                }

    /// <summary>
    /// Moves the schema from version 2 to the next version.
    /// </summary>
    /// <remarks>
    /// Kept as its own method, under the name the Orchard migration used, because the additive-only
    /// guard authorizes destructive steps per method. Folding every version into one switch would let
    /// a single authorization cover them all.
    /// </remarks>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The version the schema is now at.</returns>
    private static async Task<int> UpdateFrom2Async(ISchemaBuilder builder)
    {
                    // Adds the covering index the retention purge scans. Without it every terminating batch of the drain loop
                    // is a full scan of a table that grows with traffic, which is exactly the table size retention exists for.
                    await builder.AlterIndexTableAsync<InteractionIndex>(table => table
                        .CreateIndex(
                            "IDX_InteractionIndex_Retention",
                            "EndedUtc",
                            "DocumentId"),
                        collection: ContactCenterStorage.CollectionName);

                    return 3;
                }

    /// <summary>
    /// Moves the schema from version 3 to the next version.
    /// </summary>
    /// <remarks>
    /// Kept as its own method, under the name the Orchard migration used, because the additive-only
    /// guard authorizes destructive steps per method. Folding every version into one switch would let
    /// a single authorization cover them all.
    /// </remarks>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The version the schema is now at.</returns>
    private static async Task<int> UpdateFrom3Async(ISchemaBuilder builder)
    {
                    // Adds the predicate-led index the reservation path reads. Routing asks how much live work an agent already
                    // holds before every offer, and the existing composite leads with <c>DocumentId</c>, which serves join-back
                    // and delete-by-document but answers nothing about an agent: without this index the question is answered by
                    // scanning every interaction the contact center has ever recorded, on every routing decision.
                    await builder.AlterIndexTableAsync<InteractionIndex>(table => table
                        .CreateIndex(
                            "IDX_InteractionIndex_ActiveByAgent",
                            "AgentId",
                            "Status",
                            "DocumentId"),
                        collection: ContactCenterStorage.CollectionName);

                    return 4;
                }

    /// <summary>
    /// Moves the schema from version 4 to the next version.
    /// </summary>
    /// <remarks>
    /// Kept as its own method, under the name the Orchard migration used, because the additive-only
    /// guard authorizes destructive steps per method. Folding every version into one switch would let
    /// a single authorization cover them all.
    /// </remarks>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The version the schema is now at.</returns>
    private static async Task<int> UpdateFrom4Async(ISchemaBuilder builder)
    {
                    // Adds the legal-hold flag the retention purge filters on. Held interactions must never be fetched by the
                    // age-based purge query, because a record the policy is forbidden to delete would otherwise be re-read on every
                    // batch and could stall the drain behind a page of undeletable rows.
                    await builder.AlterIndexTableAsync<InteractionIndex>(table =>
                        table.AddColumn<bool>("RecordingLegalHold", column => column.WithDefault(false)),
                        collection: ContactCenterStorage.CollectionName
                    );

                    return 5;
                }

    /// <summary>
    /// Moves the schema from version 5 to the next version.
    /// </summary>
    /// <remarks>
    /// Kept as its own method, under the name the Orchard migration used, because the additive-only
    /// guard authorizes destructive steps per method. Folding every version into one switch would let
    /// a single authorization cover them all.
    /// </remarks>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The version the schema is now at.</returns>
    private static async Task<int> UpdateFrom5Async(ISchemaBuilder builder)
    {
                    // Adds the recording-state columns and the covering index the secure-pause auto-resume guard scans. The guard
                    // force-resumes a recording that has stayed paused past the tenant's maximum secure-pause window; without a
                    // predicate-led index on the paused state and pause time, that periodic sweep would scan every interaction the
                    // contact center has ever recorded to find the handful currently paused.
                    await builder.AlterIndexTableAsync<InteractionIndex>(table =>
                    {
                        table.AddColumn<RecordingState>("RecordingState", column => column.WithDefault((int)RecordingState.None));
                        table.AddColumn<DateTime>("RecordingPausedUtc");
                    },
                        collection: ContactCenterStorage.CollectionName
                    );

                    await builder.AlterIndexTableAsync<InteractionIndex>(table => table
                        .CreateIndex(
                            "IDX_InteractionIndex_SecurePause",
                            "RecordingState",
                            "RecordingPausedUtc",
                            "DocumentId"),
                        collection: ContactCenterStorage.CollectionName);

                    return 6;
                }
}
