using CrestApps.OrchardCore.Staff;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// Creates the portal index tables, for the Portal feature's migration and the tests.
/// </summary>
public static class PortalIndexSchema
{
    /// <summary>
    /// Creates the index tables in the <see cref="WorkforceConstants.CollectionName"/> collection.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        const string collection = WorkforceConstants.CollectionName;

        await schemaBuilder.CreateMapIndexTableAsync<WorkforceInboxIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("WorkerId", column => column.Nullable().WithLength(26))
            .Column<bool>("IsRead")
            .Column<DateTime>("CreatedUtc"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<WorkforceInboxIndex>(table =>
        {
            table.CreateIndex("IDX_WorkforceInboxIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_WorkforceInboxIndex_Lookup", "WorkerId", "IsRead", "CreatedUtc");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<WebPushSubscriptionIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("WorkerId", column => column.Nullable().WithLength(26))
            .Column<string>("EndpointHash", column => column.Nullable().WithLength(64)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<WebPushSubscriptionIndex>(table =>
        {
            table.CreateIndex("IDX_WebPushSubscriptionIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_WebPushSubscriptionIndex_Lookup", "WorkerId", "EndpointHash");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<WorkerFeedIndex>(table => table
            .Column<string>("WorkerId", column => column.Nullable().WithLength(26))
            .Column<string>("TokenHash", column => column.Nullable().WithLength(64)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<WorkerFeedIndex>(table =>
        {
            table.CreateIndex("IDX_WorkerFeedIndex_Token", "TokenHash");
        }, collection: collection);

        await CreateShiftReminderLogAsync(schemaBuilder);
    }

    /// <summary>
    /// Creates the feed secret index of staff members in the Staff collection, where workers are kept since they
    /// became staff members.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateStaffFeedIndexAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        await schemaBuilder.CreateMapIndexTableAsync<WorkerFeedIndex>(table => table
            .Column<string>("WorkerId", column => column.Nullable().WithLength(26))
            .Column<string>("TokenHash", column => column.Nullable().WithLength(64)),
            collection: StaffConstants.CollectionName);

        await schemaBuilder.AlterIndexTableAsync<WorkerFeedIndex>(table =>
        {
            table.CreateIndex("IDX_Staff_WorkerFeedIndex_Token", "TokenHash");
        }, collection: StaffConstants.CollectionName);
    }

    /// <summary>
    /// Creates the index table of sent shift reminders in the <see cref="WorkforceConstants.CollectionName"/> collection.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateShiftReminderLogAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        const string collection = WorkforceConstants.CollectionName;

        await schemaBuilder.CreateMapIndexTableAsync<ShiftReminderLogIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Key", column => column.WithLength(100))
            .Column<DateTime>("SentUtc"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<ShiftReminderLogIndex>(table =>
        {
            table.CreateIndex("IDX_ShiftReminderLogIndex_Key", "Key");
            table.CreateIndex("IDX_ShiftReminderLogIndex_SentUtc", "SentUtc");
        }, collection: collection);
    }
}
