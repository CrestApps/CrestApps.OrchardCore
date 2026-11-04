using CrestApps.OrchardCore.Outreach.Core.Indexes;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Outreach.Migrations;

/// <summary>
/// Creates the outreach run and batch index tables.
/// </summary>
internal sealed class OutreachMigrations : DataMigration
{
    /// <summary>
    /// Creates the index tables.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> CreateAsync()
    {
        await OutreachIndexSchema.CreateAsync(SchemaBuilder);

        return 1;
    }
}
