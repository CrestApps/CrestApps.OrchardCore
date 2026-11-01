using CrestApps.OrchardCore.Workforce.Core.Indexes;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Workforce.Migrations;

internal sealed class PortalMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await PortalIndexSchema.CreateAsync(SchemaBuilder);

        return 1;
    }
}
