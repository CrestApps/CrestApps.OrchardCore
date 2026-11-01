using CrestApps.OrchardCore.Workforce.ContactCenter.Indexes;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Migrations;

/// <summary>
/// Creates the forecasting index tables.
/// </summary>
internal sealed class ForecastingMigrations : DataMigration
{
    /// <summary>
    /// Creates the tables.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> CreateAsync()
    {
        await ForecastingIndexSchema.CreateAsync(SchemaBuilder);

        return 1;
    }
}
