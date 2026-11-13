using CrestApps.OrchardCore.ServiceLevels.Models;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ServiceLevels.Core.Indexes;

/// <summary>
/// Creates the index tables of the Service Levels collection, for the module's migration and for tests.
/// </summary>
public static class ServiceLevelsIndexSchema
{
    /// <summary>
    /// Creates the policy and timer index tables and their database indexes.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        const string collection = ServiceLevelsConstants.CollectionName;

        await schemaBuilder.CreateMapIndexTableAsync<ServiceLevelPolicyIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.Nullable().WithLength(255))
            .Column<string>("Scope", column => column.Nullable().WithLength(64)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<ServiceLevelPolicyIndex>(table =>
        {
            table.CreateIndex("IDX_SL_Policy_ItemId", "ItemId");
            table.CreateIndex("IDX_SL_Policy_Scope", "Scope", "Name");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<ServiceLevelTimerIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("SubjectKind", column => column.Nullable().WithLength(64))
            .Column<string>("SubjectId", column => column.Nullable().WithLength(64))
            .Column<string>("Metric", column => column.Nullable().WithLength(64))
            .Column<string>("PolicyId", column => column.Nullable().WithLength(26))
            .Column<string>("Priority", column => column.Nullable().WithLength(64))
            .Column<ServiceLevelTimerState>("State")
            .Column<bool>("IsActive")
            .Column<bool>("Warned")
            .Column<bool>("Breached")
            .Column<DateTime>("StartedUtc")
            .Column<DateTime>("WarnAtUtc")
            .Column<DateTime>("DueUtc")
            .Column<DateTime>("EndedUtc", column => column.Nullable())
            .Column<long>("TargetSeconds")
            .Column<long>("ElapsedSeconds", column => column.Nullable()),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<ServiceLevelTimerIndex>(table =>
        {
            table.CreateIndex("IDX_SL_Timer_ItemId", "ItemId");
            table.CreateIndex("IDX_SL_Timer_Subject", "SubjectKind", "SubjectId", "IsActive");

            // The monitor reads running timers in due order; reports read by start date.
            table.CreateIndex("IDX_SL_Timer_Due", "State", "Breached", "DueUtc");
            table.CreateIndex("IDX_SL_Timer_Warn", "State", "Warned", "WarnAtUtc");
            table.CreateIndex("IDX_SL_Timer_Started", "StartedUtc", "Id");
        }, collection: collection);
    }
}
