using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.AI;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.Migrations;

internal sealed class AIProfileTemplateIndexMigrations : DataMigration
{
    private readonly YesSqlStoreOptions _option;

    public AIProfileTemplateIndexMigrations(IOptions<YesSqlStoreOptions> option)
    {
        _option = option.Value;
    }

    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateAIProfileTemplateIndexSchemaAsync(_option);

        return 2;
    }

    public static Task<int> UpdateFrom1Async()
    {
        return Task.FromResult(2);
    }
}
