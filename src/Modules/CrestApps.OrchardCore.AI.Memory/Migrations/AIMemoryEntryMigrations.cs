using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.AIMemory;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.Memory.Migrations;

internal sealed class AIMemoryEntryMigrations : DataMigration
{
    private readonly YesSqlStoreOptions _option;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIMemoryEntryMigrations"/> class.
    /// </summary>
    /// <param name="option">The option.</param>
    public AIMemoryEntryMigrations(IOptions<YesSqlStoreOptions> option)
    {
        _option = option.Value;
    }

    /// <summary>
    /// Creates a new async.
    /// </summary>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateAIMemoryEntryIndexSchemaAsync(_option);

        return 2;
    }

    /// <summary>
    /// Updates the from1 async.
    /// </summary>
    public static Task<int> UpdateFrom1Async()
    {
        return Task.FromResult(2);
    }
}
