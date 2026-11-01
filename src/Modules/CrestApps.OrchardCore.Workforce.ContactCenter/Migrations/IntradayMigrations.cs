using CrestApps.OrchardCore.Workforce.ContactCenter.Indexes;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Migrations;

/// <summary>
/// Creates the intraday index tables.
/// </summary>
internal sealed class IntradayMigrations : DataMigration
{
    /// <summary>
    /// Creates the tables.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> CreateAsync()
    {
        await IntradayIndexSchema.CreateAsync(SchemaBuilder);

        return 1;
    }
}
