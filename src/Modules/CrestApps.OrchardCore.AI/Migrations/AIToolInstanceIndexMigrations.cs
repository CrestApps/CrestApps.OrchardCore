using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.Tooling;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.Migrations;

internal sealed class AIToolInstanceIndexMigrations : DataMigration
{
    private readonly YesSqlStoreOptions _option;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolInstanceIndexMigrations"/> class.
    /// </summary>
    /// <param name="option">The option.</param>
    public AIToolInstanceIndexMigrations(IOptions<YesSqlStoreOptions> option)
    {
        _option = option.Value;
    }

    /// <summary>
    /// Creates the AI tool instance index schema.
    /// </summary>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateAIToolInstanceIndexSchemaAsync(_option);

        return 1;
    }
}
