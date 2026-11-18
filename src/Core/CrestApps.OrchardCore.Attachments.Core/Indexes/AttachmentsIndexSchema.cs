using YesSql.Sql;

namespace CrestApps.OrchardCore.Attachments.Core.Indexes;

/// <summary>
/// Creates the index table of attachments.
/// </summary>
public static class AttachmentsIndexSchema
{
    /// <summary>
    /// Creates the table and its indexes.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task that completes when the table exists.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        const string collection = AttachmentsConstants.CollectionName;

        await schemaBuilder.CreateMapIndexTableAsync<AttachmentIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("OwnerKind", column => column.Nullable().WithLength(64))
            .Column<string>("OwnerId", column => column.Nullable().WithLength(64))
            .Column<string>("ContentType", column => column.Nullable().WithLength(255))
            .Column<long>("Length")
            .Column<DateTime>("UploadedUtc"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<AttachmentIndex>(table =>
        {
            table.CreateIndex("IDX_AT_Attachment_ItemId", "ItemId");
            table.CreateIndex("IDX_AT_Attachment_Owner", "OwnerKind", "OwnerId", "UploadedUtc");
        }, collection: collection);
    }
}
