using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.AI;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.Migrations;

internal sealed class AIProfileIndexMigrations : DataMigration
{
    private readonly YesSqlStoreOptions _option;

    public AIProfileIndexMigrations(IOptions<YesSqlStoreOptions> option)
    {
        _option = option.Value;
    }

    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateAIProfileIndexSchemaAsync(_option);

        return 3;
    }

    public static Task<int> UpdateFrom1Async()
    {
        return Task.FromResult(2);
    }

    public static Task<int> UpdateFrom2Async()
    {
        return Task.FromResult(3);
    }
}
