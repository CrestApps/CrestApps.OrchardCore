using CrestApps.Core.AI.Memory;
using CrestApps.Core.Data.YesSql.Indexes.AIMemory;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.Memory.Migrations;

internal sealed class AIMemoryEntryMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateAIMemoryEntryIndexSchemaAsync(MemoryConstants.CollectionName);

        return 2;
    }

    public static Task<int> UpdateFrom1Async()
    {
        return Task.FromResult(2);
    }

}
