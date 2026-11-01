using CrestApps.OrchardCore.Workforce.Models;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// Creates the time clock's index tables, for the feature's migration and for tests.
/// </summary>
public static class TimeClockIndexSchema
{
    /// <summary>
    /// Creates the tables.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        const string collection = WorkforceConstants.CollectionName;

        await schemaBuilder.CreateMapIndexTableAsync<TimeClockPolicyIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.Nullable().WithLength(255))
            .Column<bool>("Enabled"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<TimeClockPolicyIndex>(table =>
        {
            table.CreateIndex("IDX_TimeClockPolicyIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_TimeClockPolicyIndex_Lookup", "Name", "Enabled");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<PayPeriodCalendarIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.Nullable().WithLength(255))
            .Column<bool>("Enabled"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<PayPeriodCalendarIndex>(table =>
        {
            table.CreateIndex("IDX_PayPeriodCalendarIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_PayPeriodCalendarIndex_Lookup", "Name", "Enabled");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<PayCodeIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.Nullable().WithLength(255))
            .Column<string>("Code", column => column.Nullable().WithLength(50))
            .Column<PayCodeKind>("Kind")
            .Column<bool>("Enabled"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<PayCodeIndex>(table =>
        {
            table.CreateIndex("IDX_PayCodeIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_PayCodeIndex_Lookup", "Code", "Enabled");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<TimeClockKioskIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.Nullable().WithLength(255))
            .Column<string>("TokenHash", column => column.Nullable().WithLength(64))
            .Column<bool>("Enabled"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<TimeClockKioskIndex>(table =>
        {
            table.CreateIndex("IDX_TimeClockKioskIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_TimeClockKioskIndex_Token", "TokenHash");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<TimePunchIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("WorkerId", column => column.Nullable().WithLength(26))
            .Column<string>("TeamId", column => column.Nullable().WithLength(26))
            .Column<TimePunchKind>("Kind")
            .Column<DateTime>("Utc")
            .Column<string>("IdempotencyKey", column => column.Nullable().WithLength(150))
            .Column<string>("ReplacesPunchId", column => column.Nullable().WithLength(26)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<TimePunchIndex>(table =>
        {
            table.CreateIndex("IDX_TimePunchIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_TimePunchIndex_Worker", "WorkerId", "Utc");
            table.CreateIndex("IDX_TimePunchIndex_Key", "WorkerId", "IdempotencyKey");
            table.CreateIndex("IDX_TimePunchIndex_Replaces", "ReplacesPunchId");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<TimesheetIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("WorkerId", column => column.Nullable().WithLength(26))
            .Column<string>("TeamId", column => column.Nullable().WithLength(26))
            .Column<string>("PayPeriodCalendarId", column => column.Nullable().WithLength(26))
            .Column<int>("PeriodStart")
            .Column<int>("PeriodEnd")
            .Column<TimesheetStatus>("Status"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<TimesheetIndex>(table =>
        {
            table.CreateIndex("IDX_TimesheetIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_TimesheetIndex_Worker", "WorkerId", "PayPeriodCalendarId", "PeriodStart");
            table.CreateIndex("IDX_TimesheetIndex_Period", "PayPeriodCalendarId", "PeriodStart", "Status");
            table.CreateIndex("IDX_TimesheetIndex_Team", "TeamId", "Status");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<AttendanceExceptionIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("WorkerId", column => column.Nullable().WithLength(26))
            .Column<string>("TeamId", column => column.Nullable().WithLength(26))
            .Column<AttendanceExceptionKind>("Kind")
            .Column<AttendanceExceptionStatus>("Status")
            .Column<int>("Date")
            .Column<string>("ExceptionKey", column => column.Nullable().WithLength(150))
            .Column<DateTime>("CreatedUtc"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<AttendanceExceptionIndex>(table =>
        {
            table.CreateIndex("IDX_AttendanceExceptionIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_AttendanceExceptionIndex_Worker", "WorkerId", "Date");
            table.CreateIndex("IDX_AttendanceExceptionIndex_Team", "TeamId", "Status", "Date");
            table.CreateIndex("IDX_AttendanceExceptionIndex_Key", "WorkerId", "ExceptionKey");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<PunchCorrectionIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("WorkerId", column => column.Nullable().WithLength(26))
            .Column<string>("TeamId", column => column.Nullable().WithLength(26))
            .Column<PunchCorrectionStatus>("Status")
            .Column<DateTime>("CreatedUtc"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<PunchCorrectionIndex>(table =>
        {
            table.CreateIndex("IDX_PunchCorrectionIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_PunchCorrectionIndex_Team", "TeamId", "Status");
            table.CreateIndex("IDX_PunchCorrectionIndex_Worker", "WorkerId", "Status");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<PayrollExportBatchIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("ProviderName", column => column.Nullable().WithLength(100))
            .Column<string>("PayPeriodCalendarId", column => column.Nullable().WithLength(26))
            .Column<int>("PeriodStart")
            .Column<DateTime>("CreatedUtc"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<PayrollExportBatchIndex>(table =>
        {
            table.CreateIndex("IDX_PayrollExportBatchIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_PayrollExportBatchIndex_Created", "CreatedUtc");
        }, collection: collection);
    }
}
