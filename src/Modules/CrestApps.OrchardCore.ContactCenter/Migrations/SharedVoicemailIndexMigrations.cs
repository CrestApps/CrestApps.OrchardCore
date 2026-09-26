using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Models;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="SharedVoicemailIndex"/>.
/// </summary>
/// <remarks>
/// The table is new, so creating it touches no existing row and opens no second connection: it is as safe on a
/// tenant's existing SQLite database as on an empty one.
/// </remarks>
internal sealed class SharedVoicemailIndexMigrations : DataMigration
{
    /// <summary>
    /// Creates the shared voicemail index table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<SharedVoicemailIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("InteractionId", column => column.WithLength(26))
            .Column<string>("QueueId", column => column.WithLength(26))
            .Column<SharedVoicemailStatus>("Status")
            .Column<string>("ClaimedByUserId", column => column.WithLength(26))
            .Column<DateTime>("ReceivedUtc")
            .Column<DateTime>("ResolvedUtc", column => column.Nullable()),
            collection: ContactCenterStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<SharedVoicemailIndex>(table => table
            .CreateIndex("IDX_SharedVoicemailIndex_DocumentId", "DocumentId", "ItemId", "InteractionId"),
            collection: ContactCenterStorage.CollectionName
        );

        // The box is read a queue at a time, newest first, and usually without the messages already dealt with.
        await SchemaBuilder.AlterIndexTableAsync<SharedVoicemailIndex>(table => table
            .CreateIndex("IDX_SharedVoicemailIndex_Queue", "QueueId", "Status", "ReceivedUtc", "DocumentId"),
            collection: ContactCenterStorage.CollectionName
        );

        // Retention reads only the messages already dealt with, oldest first.
        await SchemaBuilder.AlterIndexTableAsync<SharedVoicemailIndex>(table => table
            .CreateIndex("IDX_SharedVoicemailIndex_Retention", "Status", "ResolvedUtc", "DocumentId"),
            collection: ContactCenterStorage.CollectionName
        );

        return 1;
    }
}
