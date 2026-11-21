using CrestApps.OrchardCore.Surveys.Core.Indexes;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Surveys.Migrations;

/// <summary>
/// Creates the index table of survey counters (evenly presented pools and quotas).
/// </summary>
internal sealed class SurveyCountersMigrations : DataMigration
{
    /// <summary>
    /// Creates the index table.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> CreateAsync()
    {
        await SurveyCountersIndexSchema.CreateAsync(SchemaBuilder);

        return 1;
    }
}
