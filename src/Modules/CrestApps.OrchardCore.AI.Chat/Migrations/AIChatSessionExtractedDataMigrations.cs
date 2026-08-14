using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.AIChat;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.Chat.Migrations;

internal sealed class AIChatSessionExtractedDataMigrations : DataMigration
{
    private readonly YesSqlStoreOptions _option;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIChatSessionExtractedDataMigrations"/> class.
    /// </summary>
    /// <param name="option">The option.</param>
    public AIChatSessionExtractedDataMigrations(IOptions<YesSqlStoreOptions> option)
    {
        _option = option.Value;
    }

    /// <summary>
    /// Creates a new async.
    /// </summary>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateAIChatSessionExtractedDataIndexSchemaAsync(_option);

        return 1;
    }
}
