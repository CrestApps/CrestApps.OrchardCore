using CrestApps.OrchardCore.Learning.Packages.Core.Cmi5;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Learning.Packages.Migrations;

/// <summary>
/// Creates the index table of cmi5 registrations.
/// </summary>
internal sealed class Cmi5Migrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await Cmi5IndexSchema.CreateAsync(SchemaBuilder);

        return 1;
    }
}
