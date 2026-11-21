using CrestApps.OrchardCore.Surveys.Core.Indexes;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Surveys.Migrations;

/// <summary>
/// Creates the index tables of Surveys.
/// </summary>
internal sealed class SurveysMigrations : DataMigration
{
    /// <summary>
    /// Creates the index tables.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> CreateAsync()
    {
        await SurveysIndexSchema.CreateAsync(SchemaBuilder);

        return 1;
    }
}
