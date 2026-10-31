using CrestApps.OrchardCore.Workforce.Models;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// Creates the Workforce index tables, for the module's migration and the tests.
/// </summary>
public static class WorkforceIndexSchema
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

        await schemaBuilder.CreateMapIndexTableAsync<WorkerIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("DisplayName", column => column.Nullable().WithLength(255))
            .Column<string>("DisplayText", column => column.Nullable().WithLength(255))
            .Column<string>("NormalizedEmployeeNumber", column => column.Nullable().WithLength(100))
            .Column<string>("UserId", column => column.Nullable().WithLength(26))
            .Column<string>("Email", column => column.Nullable().WithLength(255))
            .Column<WorkerStatus>("Status")
            .Column<EmploymentType>("EmploymentType")
            .Column<string>("PrimaryTeamId", column => column.Nullable().WithLength(26)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<WorkerIndex>(table =>
        {
            table.CreateIndex("IDX_WorkerIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_WorkerIndex_UserId", "UserId");
            table.CreateIndex("IDX_WorkerIndex_EmployeeNumber", "NormalizedEmployeeNumber");
            table.CreateIndex("IDX_WorkerIndex_Name", "Status", "DisplayName");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<WorkerTeamMembershipIndex>(table => table
            .Column<string>("WorkerId", column => column.WithLength(26))
            .Column<string>("TeamId", column => column.WithLength(26))
            .Column<TeamMemberRole>("Role")
            .Column<int>("EffectiveFrom", column => column.Nullable())
            .Column<int>("EffectiveTo", column => column.Nullable()),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<WorkerTeamMembershipIndex>(table =>
        {
            table.CreateIndex("IDX_WorkerTeamMembershipIndex_Team", "TeamId", "Role");
            table.CreateIndex("IDX_WorkerTeamMembershipIndex_Worker", "WorkerId");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<TeamIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.Nullable().WithLength(255))
            .Column<TeamKind>("Kind")
            .Column<string>("ParentId", column => column.Nullable().WithLength(26))
            .Column<bool>("Enabled"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<TeamIndex>(table =>
        {
            table.CreateIndex("IDX_TeamIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_TeamIndex_Parent", "ParentId");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<ActivityCodeIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.Nullable().WithLength(255))
            .Column<ActivityKind>("Kind")
            .Column<bool>("Enabled"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<ActivityCodeIndex>(table => table.CreateIndex("IDX_ActivityCodeIndex_ItemId", "ItemId"), collection: collection);
    }
}
