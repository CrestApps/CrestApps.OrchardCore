using CrestApps.OrchardCore.Telnyx.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Telnyx.Migrations;

/// <summary>
/// Creates the schema for the durable <see cref="TelnyxRecordingIngestJobIndex"/> that tracks secure Telnyx
/// recording ingestion progress per tenant. This is a schema migration for a new durable store and is expected;
/// it does not alter any existing data.
/// </summary>
public sealed class TelnyxRecordingIngestJobMigrations : DataMigration
{
    /// <summary>
    /// Creates the recording ingest job index table and its supporting indexes.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<TelnyxRecordingIngestJobIndex>(table => table
            .Column<string>("RecordingId", column => column.WithLength(128))
            .Column<int>("Status")
            .Column<DateTime>("NextAttemptUtc")
        );

        await SchemaBuilder.AlterIndexTableAsync<TelnyxRecordingIngestJobIndex>(table => table
            .CreateIndex("IDX_TelnyxRecordingIngestJobIndex_RecordingId",
                "RecordingId",
                "DocumentId")
        );

        await SchemaBuilder.AlterIndexTableAsync<TelnyxRecordingIngestJobIndex>(table => table
            .CreateIndex("IDX_TelnyxRecordingIngestJobIndex_Due",
                "Status",
                "NextAttemptUtc",
                "DocumentId")
        );

        return 1;
    }
}
