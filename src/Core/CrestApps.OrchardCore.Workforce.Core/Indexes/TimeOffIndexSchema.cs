using CrestApps.OrchardCore.Workforce.Models;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// Creates the time-off index tables, for the Time Off feature's migration and the tests.
/// </summary>
public static class TimeOffIndexSchema
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

        await schemaBuilder.CreateMapIndexTableAsync<TimeOffTypeIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.Nullable().WithLength(255))
            .Column<bool>("Enabled"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<TimeOffTypeIndex>(table =>
        {
            table.CreateIndex("IDX_TimeOffTypeIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_TimeOffTypeIndex_Lookup", "Name", "Enabled");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<TimeOffPolicyIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.Nullable().WithLength(255))
            .Column<string>("TypeId", column => column.Nullable().WithLength(26))
            .Column<TimeOffAccrualMethod>("AccrualMethod")
            .Column<bool>("Enabled"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<TimeOffPolicyIndex>(table =>
        {
            table.CreateIndex("IDX_TimeOffPolicyIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_TimeOffPolicyIndex_Lookup", "TypeId", "Enabled");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<TimeOffPolicyAssignmentIndex>(table => table
            .Column<string>("PolicyId", column => column.Nullable().WithLength(26))
            .Column<string>("TeamId", column => column.Nullable().WithLength(26))
            .Column<string>("WorkerId", column => column.Nullable().WithLength(26)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<TimeOffPolicyAssignmentIndex>(table =>
        {
            table.CreateIndex("IDX_TimeOffPolicyAssignment_Team", "TeamId");
            table.CreateIndex("IDX_TimeOffPolicyAssignment_Worker", "WorkerId");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<TimeOffLedgerEntryIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("WorkerId", column => column.Nullable().WithLength(26))
            .Column<string>("PolicyId", column => column.Nullable().WithLength(26))
            .Column<TimeOffLedgerKind>("Kind")
            .Column<int>("EffectiveDate")
            .Column<string>("IdempotencyKey", column => column.Nullable().WithLength(150))
            .Column<DateTime>("CreatedUtc"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<TimeOffLedgerEntryIndex>(table =>
        {
            table.CreateIndex("IDX_TimeOffLedgerEntryIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_TimeOffLedgerEntryIndex_Lookup", "WorkerId", "PolicyId", "EffectiveDate");
            table.CreateIndex("IDX_TimeOffLedgerEntryIndex_Key", "IdempotencyKey");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<TimeOffRequestIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("WorkerId", column => column.Nullable().WithLength(26))
            .Column<string>("TeamId", column => column.Nullable().WithLength(26))
            .Column<string>("TypeId", column => column.Nullable().WithLength(26))
            .Column<string>("PolicyId", column => column.Nullable().WithLength(26))
            .Column<TimeOffRequestStatus>("Status")
            .Column<int>("StartDate")
            .Column<int>("EndDate")
            .Column<DateTime>("SubmittedUtc"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<TimeOffRequestIndex>(table =>
        {
            table.CreateIndex("IDX_TimeOffRequestIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_TimeOffRequestIndex_Team", "TeamId", "Status", "StartDate");
            table.CreateIndex("IDX_TimeOffRequestIndex_Worker", "WorkerId", "Status", "StartDate");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<TimeOffAllotmentIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.Nullable().WithLength(255))
            .Column<string>("TeamId", column => column.Nullable().WithLength(26))
            .Column<bool>("Enabled"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<TimeOffAllotmentIndex>(table =>
        {
            table.CreateIndex("IDX_TimeOffAllotmentIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_TimeOffAllotmentIndex_Lookup", "TeamId", "Enabled");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<BlackoutPeriodIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.Nullable().WithLength(255))
            .Column<string>("TeamId", column => column.Nullable().WithLength(26))
            .Column<int>("StartDate")
            .Column<int>("EndDate")
            .Column<bool>("Enabled"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<BlackoutPeriodIndex>(table =>
        {
            table.CreateIndex("IDX_BlackoutPeriodIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_BlackoutPeriodIndex_Lookup", "StartDate", "EndDate");
        }, collection: collection);
    }
}
