using CrestApps.OrchardCore.Dispatch.Core.Indexes;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Dispatch.Migrations;

internal sealed class DispatchMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await DispatchIndexSchema.CreateAsync(SchemaBuilder);

        return 1;
    }
}
