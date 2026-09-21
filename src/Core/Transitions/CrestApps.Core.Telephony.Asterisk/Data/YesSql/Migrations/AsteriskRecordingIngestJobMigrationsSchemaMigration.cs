using CrestApps.Core.Data.YesSql.Migrations;
using CrestApps.Core.Telephony.Asterisk.Data.YesSql.Indexes;
using YesSql.Sql;

namespace CrestApps.Core.Telephony.Asterisk.Data.YesSql.Migrations;

/// <summary>
/// Creates the schema for the durable <see cref="AsteriskRecordingIngestJobIndex"/> that tracks secure
/// recording ingestion progress per tenant. This is a schema migration for a new durable store and is
/// expected; it does not alter any existing data.
/// </summary>
public sealed class AsteriskRecordingIngestJobMigrationsSchemaMigration : ISchemaMigration
{
    /// <inheritdoc/>
    /// <remarks>
    /// The stored name is the Orchard migration class's own name, so a database migrated under either
    /// host agrees on which version has already been applied.
    /// </remarks>
    public string Name => "AsteriskRecordingIngestJobMigrations";

    /// <summary>
    /// Creates the recording ingest job index table and its supporting indexes.
    /// </summary>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync(ISchemaBuilder builder)
    {
        await builder.CreateMapIndexTableAsync<AsteriskRecordingIngestJobIndex>(table => table
            .Column<string>("RecordingName", column => column.WithLength(128))
            .Column<int>("Status")
            .Column<DateTime>("NextAttemptUtc")
        );

        await builder.AlterIndexTableAsync<AsteriskRecordingIngestJobIndex>(table => table
            .CreateIndex("IDX_AsteriskRecordingIngestJobIndex_RecordingName",
                "RecordingName",
                "DocumentId")
        );

        await builder.AlterIndexTableAsync<AsteriskRecordingIngestJobIndex>(table => table
            .CreateIndex("IDX_AsteriskRecordingIngestJobIndex_Due",
                "Status",
                "NextAttemptUtc",
                "DocumentId")
        );

        return 1;
    }

    /// <summary>
    /// Moves the schema forward from an already-applied version. There is no step past the create, so the
    /// applied version is returned unchanged.
    /// </summary>
    /// <param name="version">The version currently applied.</param>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The version the schema is now at.</returns>
    public Task<int> UpdateFromAsync(int version, ISchemaBuilder builder)
        => Task.FromResult(version);
}
