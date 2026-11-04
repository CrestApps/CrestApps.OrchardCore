using CrestApps.OrchardCore.Dispatch.Core.Indexes;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Dispatch.Migrations;

internal sealed class FieldMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await FieldIndexSchema.CreateAsync(SchemaBuilder);

        return 1;
    }
}
