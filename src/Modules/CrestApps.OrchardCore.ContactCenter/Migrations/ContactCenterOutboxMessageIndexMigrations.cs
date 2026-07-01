using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="ContactCenterOutboxMessageIndex"/>.
/// </summary>
internal sealed class ContactCenterOutboxMessageIndexMigrations : DataMigration
{
    /// <summary>
    /// Creates the outbox message index table and its supporting index.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<ContactCenterOutboxMessageIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("EventId", column => column.WithLength(26))
            .Column<string>("Status", column => column.WithLength(50))
            .Column<DateTime>("NextAttemptUtc", column => column.NotNull()),
            collection: ContactCenterConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<ContactCenterOutboxMessageIndex>(table => table
            .CreateIndex("IDX_ContactCenterOutboxMessageIndex_Due",
                "DocumentId",
                "Status",
                "NextAttemptUtc"),
            collection: ContactCenterConstants.CollectionName
        );

        return 1;
    }
}
