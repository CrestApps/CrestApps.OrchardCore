using YesSql.Sql;

namespace CrestApps.OrchardCore.Learning.Programs.Core.Indexes;

/// <summary>
/// Creates the tables of Learning programs.
/// </summary>
public static class ProgramsIndexSchema
{
    /// <summary>
    /// Creates the index tables.
    /// </summary>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        const string collection = ProgramsConstants.CollectionName;

        await schemaBuilder.CreateMapIndexTableAsync<LearningPathProgressIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("PathId", column => column.WithLength(26))
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<int>("State")
            .Column<DateTime>("CompletedUtc", column => column.Nullable()),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<LearningPathProgressIndex>(table =>
        {
            table.CreateIndex("IDX_LearningPathProgress_ItemId", "ItemId");
            table.CreateIndex("IDX_LearningPathProgress_PathUser", "PathId", "UserId");
            table.CreateIndex("IDX_LearningPathProgress_User", "UserId");
            table.CreateIndex("IDX_LearningPathProgress_PathState", "PathId", "State");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<LearningRequirementIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("TargetKind", column => column.WithLength(50))
            .Column<string>("TargetId", column => column.WithLength(26))
            .Column<bool>("Enabled"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<LearningRequirementIndex>(table =>
        {
            table.CreateIndex("IDX_LearningRequirement_ItemId", "ItemId");
            table.CreateIndex("IDX_LearningRequirement_Target", "TargetKind", "TargetId", "Enabled");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<RequiredLearningAssignmentIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("RequirementId", column => column.WithLength(26))
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<int>("State")
            .Column<DateTime>("DueUtc", column => column.Nullable()),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<RequiredLearningAssignmentIndex>(table =>
        {
            table.CreateIndex("IDX_RequiredLearning_ItemId", "ItemId");
            table.CreateIndex("IDX_RequiredLearning_RequirementUser", "RequirementId", "UserId");
            table.CreateIndex("IDX_RequiredLearning_User", "UserId");
            table.CreateIndex("IDX_RequiredLearning_RequirementState", "RequirementId", "State");
            table.CreateIndex("IDX_RequiredLearning_StateDue", "State", "DueUtc", "Id");
        }, collection: collection);
    }
}
