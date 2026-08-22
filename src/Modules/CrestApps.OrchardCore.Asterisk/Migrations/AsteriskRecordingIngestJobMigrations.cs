using CrestApps.OrchardCore.Asterisk.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Asterisk.Migrations;

/// <summary>
/// Creates the schema for the durable <see cref="AsteriskRecordingIngestJobIndex"/> that tracks secure
/// recording ingestion progress per tenant. This is a schema migration for a new durable store and is
/// expected; it does not alter any existing data.
/// </summary>
public sealed class AsteriskRecordingIngestJobMigrations : DataMigration
{
    /// <summary>
    /// Creates the recording ingest job index table and its supporting indexes.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<AsteriskRecordingIngestJobIndex>(table => table
            .Column<string>("RecordingName", column => column.WithLength(128))
            .Column<int>("Status")
            .Column<DateTime>("NextAttemptUtc")
        );

        await SchemaBuilder.AlterIndexTableAsync<AsteriskRecordingIngestJobIndex>(table => table
            .CreateIndex("IDX_AsteriskRecordingIngestJobIndex_RecordingName",
                "RecordingName",
                "DocumentId")
        );

        await SchemaBuilder.AlterIndexTableAsync<AsteriskRecordingIngestJobIndex>(table => table
            .CreateIndex("IDX_AsteriskRecordingIngestJobIndex_Due",
                "Status",
                "NextAttemptUtc",
                "DocumentId")
        );

        return 1;
    }
}
