using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.A2A;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.A2A.Migrations;

internal sealed class A2AConnectionMigrations : DataMigration
{
    private readonly YesSqlStoreOptions _options;

    public A2AConnectionMigrations(IOptions<YesSqlStoreOptions> options)
    {
        _options = options.Value;
    }

    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateA2AConnectionIndexSchemaAsync(_options);

        return 1;
    }
}
