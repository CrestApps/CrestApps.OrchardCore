using CrestApps.OrchardCore.Learning.Cohorts;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Learning.Migrations;

/// <summary>
/// Creates the cohort link index table.
/// </summary>
internal sealed class LearningCohortsMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<CohortLearningLinkIndex>(table => table
            .Column<string>("TargetId", column => column.WithLength(26))
            .Column<string>("CohortId", column => column.WithLength(50)),
            collection: LearningConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<CohortLearningLinkIndex>(table =>
        {
            table.CreateIndex("IDX_Learning_CohortLearningLinkIndex_Cohort", "CohortId", "TargetId");
            table.CreateIndex("IDX_Learning_CohortLearningLinkIndex_Target", "TargetId");
        }, collection: LearningConstants.CollectionName);

        return 1;
    }
}
