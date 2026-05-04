using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.AIChat;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.Migrations;

internal sealed class AICompletionUsageIndexMigrations : DataMigration
{
    private readonly YesSqlStoreOptions _option;

    /// <summary>
    /// Initializes a new instance of the <see cref="AICompletionUsageIndexMigrations"/> class.
    /// </summary>
    /// <param name="option">The option.</param>
    public AICompletionUsageIndexMigrations(IOptions<YesSqlStoreOptions> option)
    {
        _option = option.Value;
    }

    /// <summary>
    /// Creates a new async.
    /// </summary>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateAICompletionUsageIndexSchemaAsync(_option);

        return 1;
    }
}
