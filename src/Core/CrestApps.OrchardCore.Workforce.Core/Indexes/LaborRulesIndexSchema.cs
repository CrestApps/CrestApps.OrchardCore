using CrestApps.OrchardCore.Workforce.Models;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// Creates the labor rule index tables, for the Labor Rules feature's migration and the tests.
/// </summary>
public static class LaborRulesIndexSchema
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

        await schemaBuilder.CreateMapIndexTableAsync<LaborRuleSetIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.Nullable().WithLength(255))
            .Column<bool>("Enabled"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<LaborRuleSetIndex>(table =>
        {
            table.CreateIndex("IDX_LaborRuleSetIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_LaborRuleSetIndex_Lookup", "Name", "Enabled");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<LaborRuleOverrideIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("TeamId", column => column.Nullable().WithLength(26))
            .Column<string>("WorkerId", column => column.Nullable().WithLength(26))
            .Column<DateTime>("CreatedUtc"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<LaborRuleOverrideIndex>(table =>
        {
            table.CreateIndex("IDX_LaborRuleOverrideIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_LaborRuleOverrideIndex_Lookup", "TeamId", "CreatedUtc");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<PredictabilityPayEntryIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("TeamId", column => column.Nullable().WithLength(26))
            .Column<string>("WorkerId", column => column.Nullable().WithLength(26))
            .Column<string>("ScheduleChangeId", column => column.Nullable().WithLength(26))
            .Column<PredictabilityPayStatus>("Status")
            .Column<DateTime>("CreatedUtc"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<PredictabilityPayEntryIndex>(table =>
        {
            table.CreateIndex("IDX_PredictabilityPayEntryIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_PredictabilityPayEntryIndex_Lookup", "TeamId", "Status", "CreatedUtc");
            table.CreateIndex("IDX_PredictabilityPayEntryIndex_Change", "ScheduleChangeId");
        }, collection: collection);
    }
}
