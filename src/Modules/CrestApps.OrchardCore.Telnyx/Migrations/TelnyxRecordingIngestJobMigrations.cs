using CrestApps.OrchardCore.Telnyx.Core.Migrations;
using CrestApps.OrchardCore.Telnyx.Indexes;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Telnyx.Migrations;

/// <summary>
/// Creates the schema for the durable <see cref="TelnyxRecordingIngestJobIndex"/> that tracks secure Telnyx
/// recording ingestion progress per tenant. This is a schema migration for a new durable store and is expected;
/// it does not alter any existing data.
/// </summary>
/// <remarks>
/// The schema itself lives in <see cref="TelnyxRecordingIngestJobMigrationsSchemaMigration"/>; this class only
/// hands Orchard's schema builder to it. The type name is kept because Orchard records the applied version
/// under this class's full type name, so renaming it would run the create step against existing tables.
/// </remarks>
public sealed class TelnyxRecordingIngestJobMigrations : DataMigration
{
    private readonly TelnyxRecordingIngestJobMigrationsSchemaMigration _step = new();

    /// <summary>
    /// Creates the recording ingest job index table and its supporting indexes.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> CreateAsync()
        => _step.CreateAsync(SchemaBuilder);
}
