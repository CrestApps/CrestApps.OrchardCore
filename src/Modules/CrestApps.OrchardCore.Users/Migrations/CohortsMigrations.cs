using CrestApps.OrchardCore.Users.Core.Cohorts;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Users.Migrations;

/// <summary>
/// Creates the cohort index tables.
/// </summary>
internal sealed class CohortsMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await CohortsIndexSchema.CreateAsync(SchemaBuilder);

        return 1;
    }
}
