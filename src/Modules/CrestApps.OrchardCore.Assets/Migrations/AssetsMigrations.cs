using CrestApps.OrchardCore.Assets.Core.Indexes;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Assets.Migrations;

/// <summary>
/// Creates the Assets index tables.
/// </summary>
internal sealed class AssetsMigrations : DataMigration
{
    /// <summary>
    /// Creates the tables.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> CreateAsync()
    {
        await AssetsIndexSchema.CreateAsync(SchemaBuilder);

        return 1;
    }
}
