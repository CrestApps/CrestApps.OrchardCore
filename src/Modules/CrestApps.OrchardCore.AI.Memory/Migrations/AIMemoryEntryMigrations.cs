using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.AIMemory;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.Memory.Migrations;

internal sealed class AIMemoryEntryMigrations : DataMigration
{
    private readonly YesSqlStoreOptions _option;

    public AIMemoryEntryMigrations(IOptions<YesSqlStoreOptions> option)
    {
        _option = option.Value;
    }

    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateAIMemoryEntryIndexSchemaAsync(_option);

        return 2;
    }

    public static Task<int> UpdateFrom1Async()
    {
        return Task.FromResult(2);
    }
}
