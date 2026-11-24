using CrestApps.OrchardCore.Timeline.Models;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Timeline.Core.Indexes;

/// <summary>
/// Creates the index table of timeline entries.
/// </summary>
public static class TimelineIndexSchema
{
    /// <summary>
    /// Creates the table and its indexes.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task that completes when the table exists.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        const string collection = TimelineConstants.CollectionName;

        await schemaBuilder.CreateMapIndexTableAsync<TimelineEntryIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("SubjectKind", column => column.Nullable().WithLength(64))
            .Column<string>("SubjectId", column => column.Nullable().WithLength(64))
            .Column<string>("Kind", column => column.Nullable().WithLength(64))
            .Column<TimelineVisibility>("Visibility")
            .Column<string>("AuthorKind", column => column.Nullable().WithLength(64))
            .Column<string>("AuthorId", column => column.Nullable().WithLength(64))
            .Column<string>("Channel", column => column.Nullable().WithLength(64))
            .Column<string>("ExternalId", column => column.Nullable().WithLength(TimelineConstants.MaxExternalIdLength))
            .Column<DateTime>("CreatedUtc")
            .Column<bool>("Redacted"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<TimelineEntryIndex>(table =>
        {
            table.CreateIndex("IDX_TL_Entry_ItemId", "ItemId");

            // A record's timeline is read newest first; reports read by time.
            table.CreateIndex("IDX_TL_Entry_Subject", "SubjectKind", "SubjectId", "CreatedUtc");
            table.CreateIndex("IDX_TL_Entry_Created", "CreatedUtc", "Kind");

            // A channel finds an entry again by the id it has outside the site, such as an e-mail's message id.
            table.CreateIndex("IDX_TL_Entry_External", "ExternalId");
        }, collection: collection);

        await CreateAttachmentIndexAsync(schemaBuilder);
    }

    /// <summary>
    /// Creates the table of the files attached to entries, one row per file.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task that completes when the table exists.</returns>
    public static async Task CreateAttachmentIndexAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        const string collection = TimelineConstants.CollectionName;

        await schemaBuilder.CreateMapIndexTableAsync<TimelineAttachmentIndex>(table => table
            .Column<string>("AttachmentId", column => column.WithLength(26))
            .Column<string>("EntryId", column => column.WithLength(26))
            .Column<string>("SubjectKind", column => column.Nullable().WithLength(64))
            .Column<string>("SubjectId", column => column.Nullable().WithLength(64))
            .Column<string>("Kind", column => column.Nullable().WithLength(64))
            .Column<TimelineVisibility>("Visibility")
            .Column<string>("AuthorName", column => column.Nullable().WithLength(255))
            .Column<DateTime>("CreatedUtc")
            .Column<bool>("Redacted"),
            collection: collection);

        // A record's files are read newest first.
        await schemaBuilder.AlterIndexTableAsync<TimelineAttachmentIndex>(
            table => table.CreateIndex("IDX_TL_File_Subject", "SubjectKind", "SubjectId", "Redacted", "CreatedUtc"),
            collection: collection);
    }
}
