using CrestApps.OrchardCore.Workforce.Models;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// Creates the shift planning index tables, for the Shift Planning feature's migration and the tests.
/// </summary>
public static class ShiftPlanningIndexSchema
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

        await schemaBuilder.CreateMapIndexTableAsync<ShiftTemplateIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.Nullable().WithLength(255))
            .Column<bool>("Enabled"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<ShiftTemplateIndex>(table =>
        {
            table.CreateIndex("IDX_ShiftTemplateIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_ShiftTemplateIndex_Lookup", "Name", "Enabled");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<ShiftPatternIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.Nullable().WithLength(255))
            .Column<string>("TeamId", column => column.Nullable().WithLength(26))
            .Column<bool>("Enabled"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<ShiftPatternIndex>(table =>
        {
            table.CreateIndex("IDX_ShiftPatternIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_ShiftPatternIndex_Lookup", "Name", "TeamId");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<CoverageRequirementIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("TeamId", column => column.Nullable().WithLength(26))
            .Column<string>("SkillId", column => column.Nullable().WithLength(26)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<CoverageRequirementIndex>(table =>
        {
            table.CreateIndex("IDX_CoverageRequirementIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_CoverageRequirementIndex_Lookup", "TeamId", "SkillId");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<SchedulePeriodIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("TeamId", column => column.Nullable().WithLength(26))
            .Column<int>("StartDate")
            .Column<int>("EndDate")
            .Column<SchedulePeriodStatus>("Status"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<SchedulePeriodIndex>(table =>
        {
            table.CreateIndex("IDX_SchedulePeriodIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_SchedulePeriodIndex_Lookup", "TeamId", "StartDate");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<ScheduleScenarioIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("PeriodId", column => column.Nullable().WithLength(26)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<ScheduleScenarioIndex>(table =>
        {
            table.CreateIndex("IDX_ScheduleScenarioIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_ScheduleScenarioIndex_Lookup", "PeriodId");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<DraftShiftIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("ScenarioId", column => column.Nullable().WithLength(26))
            .Column<string>("PeriodId", column => column.Nullable().WithLength(26))
            .Column<string>("WorkerId", column => column.Nullable().WithLength(26))
            .Column<int>("Date"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<DraftShiftIndex>(table =>
        {
            table.CreateIndex("IDX_DraftShiftIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_DraftShiftIndex_Lookup", "ScenarioId", "PeriodId");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<ScheduleChangeIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("PeriodId", column => column.Nullable().WithLength(26))
            .Column<string>("TeamId", column => column.Nullable().WithLength(26))
            .Column<string>("WorkerId", column => column.Nullable().WithLength(26))
            .Column<DateTime>("ChangedUtc"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<ScheduleChangeIndex>(table =>
        {
            table.CreateIndex("IDX_ScheduleChangeIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_ScheduleChangeIndex_Lookup", "PeriodId", "TeamId");
        }, collection: collection);
    }
}
