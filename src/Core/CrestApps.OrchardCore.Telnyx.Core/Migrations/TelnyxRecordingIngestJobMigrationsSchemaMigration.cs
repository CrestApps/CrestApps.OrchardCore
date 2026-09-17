using CrestApps.Core.Data.YesSql.Migrations;
using CrestApps.OrchardCore.Telnyx.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Telnyx.Core.Migrations;

/// <summary>
/// Creates the schema for the durable <see cref="TelnyxRecordingIngestJobIndex"/> that tracks secure Telnyx
/// recording ingestion progress per tenant. This is a schema migration for a new durable store and is expected;
/// it does not alter any existing data.
/// </summary>
internal sealed class TelnyxRecordingIngestJobMigrationsSchemaMigration : ISchemaMigration
{
    /// <inheritdoc/>
    /// <remarks>
    /// The stored name is the Orchard migration class's own name, so a database migrated under either
    /// host agrees on which version has already been applied.
    /// </remarks>
    public string Name => "TelnyxRecordingIngestJobMigrations";

    /// <summary>
    /// Creates the recording ingest job index table and its supporting indexes.
    /// </summary>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync(ISchemaBuilder builder)
    {
        await builder.CreateMapIndexTableAsync<TelnyxRecordingIngestJobIndex>(table => table
            .Column<string>("RecordingId", column => column.WithLength(128))
            .Column<int>("Status")
            .Column<DateTime>("NextAttemptUtc")
        );

        await builder.AlterIndexTableAsync<TelnyxRecordingIngestJobIndex>(table => table
            .CreateIndex("IDX_TelnyxRecordingIngestJobIndex_RecordingId",
                "RecordingId",
                "DocumentId")
        );

        await builder.AlterIndexTableAsync<TelnyxRecordingIngestJobIndex>(table => table
            .CreateIndex("IDX_TelnyxRecordingIngestJobIndex_Due",
                "Status",
                "NextAttemptUtc",
                "DocumentId")
        );

        return 1;
    }

    /// <summary>
    /// Moves the schema forward from an already-applied version. There is no step past the create, so
    /// the applied version is returned unchanged.
    /// </summary>
    /// <param name="version">The version currently applied.</param>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The version the schema is now at.</returns>
    public Task<int> UpdateFromAsync(int version, ISchemaBuilder builder)
        => Task.FromResult(version);
}
