using CrestApps.OrchardCore.Geo.Core.Indexes;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Geo.Migrations;

/// <summary>
/// Creates the Geo index tables.
/// </summary>
internal sealed class GeoMigrations : DataMigration
{
    /// <summary>
    /// Creates the tables.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> CreateAsync()
    {
        await GeoIndexSchema.CreateAsync(SchemaBuilder);

        return 1;
    }
}
