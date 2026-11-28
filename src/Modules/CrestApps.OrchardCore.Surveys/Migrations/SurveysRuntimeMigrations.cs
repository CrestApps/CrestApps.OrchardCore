using CrestApps.OrchardCore.Surveys.Core.Runtime;
using CrestApps.OrchardCore.YesSql.Core.WorkQueues;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Surveys.Migrations;

/// <summary>
/// Creates the index of the deadline work queue.
/// </summary>
internal sealed class SurveysRuntimeMigrations : DataMigration
{
    /// <summary>
    /// Creates the index.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> CreateAsync()
    {
        await LeasedWorkItemIndexSchema.CreateAsync(SchemaBuilder, SurveyDeadlineWork.CollectionName);

        return 1;
    }
}
