using CrestApps.OrchardCore.Surveys.Core.Indexes;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Surveys.Migrations;

/// <summary>
/// Creates the answer index table, which results aggregate in SQL.
/// </summary>
internal sealed class SurveyAnswerIndexMigrations : DataMigration
{
    /// <summary>
    /// Creates the table.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> CreateAsync()
    {
        await SurveyAnswerIndexSchema.CreateAsync(SchemaBuilder);

        return 1;
    }
}
