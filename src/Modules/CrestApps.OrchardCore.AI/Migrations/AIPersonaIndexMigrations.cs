using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.AI;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.Migrations;

internal sealed class AIPersonaIndexMigrations : DataMigration
{
    private readonly YesSqlStoreOptions _option;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIPersonaIndexMigrations"/> class.
    /// </summary>
    /// <param name="option">The option.</param>
    public AIPersonaIndexMigrations(IOptions<YesSqlStoreOptions> option)
    {
        _option = option.Value;
    }

    /// <summary>
    /// Creates the AI persona index schema.
    /// </summary>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateAIPersonaIndexSchemaAsync(_option);

        return 1;
    }
}
