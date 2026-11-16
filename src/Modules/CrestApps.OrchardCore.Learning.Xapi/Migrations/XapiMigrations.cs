using CrestApps.OrchardCore.Learning.Xapi.Core.Indexes;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Learning.Xapi.Migrations;

/// <summary>
/// Creates the index tables of statements, documents and activity definitions.
/// </summary>
internal sealed class XapiMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await XapiIndexSchema.CreateAllAsync(SchemaBuilder);

        return 1;
    }
}
