using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.YesSql.Core.WorkQueues;
using YesSql.Sql;

namespace CrestApps.OrchardCore.MailingLists.Core.Indexes;

/// <summary>
/// Creates the mailing list tables: lists, members and the provider outbox.
/// </summary>
public static class MailingListsIndexSchema
{
    /// <summary>
    /// The longest list name kept in the index.
    /// </summary>
    public const int NameLength = 255;

    /// <summary>
    /// The longest provider name kept in the index.
    /// </summary>
    public const int ProviderNameLength = 64;

    /// <summary>
    /// The longest e-mail address kept in the index (the RFC 5321 path limit).
    /// </summary>
    public const int EmailLength = 254;

    /// <summary>
    /// The longest member source kept in the index.
    /// </summary>
    public const int SourceLength = 64;

    /// <summary>
    /// Creates the tables and their indexes.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task that completes when the tables exist.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        const string collection = MailingListsConstants.CollectionName;

        await schemaBuilder.CreateMapIndexTableAsync<MailingListIndex>(table => table
            .Column<string>(nameof(MailingListIndex.ItemId), column => column.WithLength(26))
            .Column<string>(nameof(MailingListIndex.Name), column => column.Nullable().WithLength(NameLength))
            .Column<string>(nameof(MailingListIndex.ProviderName), column => column.Nullable().WithLength(ProviderNameLength))
            .Column<bool>(nameof(MailingListIndex.IsEnabled)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<MailingListIndex>(table =>
        {
            table.CreateIndex("IDX_MailingListIndex_ItemId", nameof(MailingListIndex.ItemId), "DocumentId");
            table.CreateIndex("IDX_MailingListIndex_Name", nameof(MailingListIndex.Name), "DocumentId");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<MailingListMemberIndex>(table => table
            .Column<string>(nameof(MailingListMemberIndex.ItemId), column => column.WithLength(26))
            .Column<string>(nameof(MailingListMemberIndex.ListId), column => column.WithLength(26))
            .Column<string>(nameof(MailingListMemberIndex.NormalizedEmail), column => column.Nullable().WithLength(EmailLength))
            .Column<MailingListMemberStatus>(nameof(MailingListMemberIndex.Status))
            .Column<string>(nameof(MailingListMemberIndex.UserId), column => column.Nullable().WithLength(26))
            .Column<DateTime>(nameof(MailingListMemberIndex.SubscribedUtc), column => column.Nullable())
            .Column<DateTime>(nameof(MailingListMemberIndex.StatusChangedUtc))
            .Column<string>(nameof(MailingListMemberIndex.Source), column => column.Nullable().WithLength(SourceLength))
            .Column<MailingListSyncState>(nameof(MailingListMemberIndex.SyncState)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<MailingListMemberIndex>(table =>
        {
            table.CreateIndex("IDX_MailingListMember_ItemId", nameof(MailingListMemberIndex.ItemId), "DocumentId");
            table.CreateIndex("IDX_MailingListMember_ListEmail", nameof(MailingListMemberIndex.ListId), nameof(MailingListMemberIndex.NormalizedEmail), "DocumentId");
            table.CreateIndex("IDX_MailingListMember_Email", nameof(MailingListMemberIndex.NormalizedEmail), "DocumentId");
            table.CreateIndex("IDX_MailingListMember_ListStatus", nameof(MailingListMemberIndex.ListId), nameof(MailingListMemberIndex.Status), "DocumentId");
            table.CreateIndex("IDX_MailingListMember_StatusAge", nameof(MailingListMemberIndex.Status), nameof(MailingListMemberIndex.StatusChangedUtc), "DocumentId");
            table.CreateIndex("IDX_MailingListMember_Sync", nameof(MailingListMemberIndex.ListId), nameof(MailingListMemberIndex.SyncState), "DocumentId");
            table.CreateIndex("IDX_MailingListMember_User", nameof(MailingListMemberIndex.UserId), "DocumentId");
        }, collection: collection);

        await LeasedWorkItemIndexSchema.CreateAsync(schemaBuilder, collection);
    }

    /// <summary>
    /// Cuts a value to the length of its index column.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="length">The column length.</param>
    /// <returns>The value, cut when it is longer.</returns>
    internal static string Cut(string value, int length)
        => value is not null && value.Length > length ? value[..length] : value;
}
