using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.Mcp;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.Mcp.Migrations;

internal sealed class McpConnectionServerMigrations : DataMigration
{
    private readonly YesSqlStoreOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpConnectionServerMigrations"/> class.
    /// </summary>
    /// <param name="options">The options.</param>
    public McpConnectionServerMigrations(IOptions<YesSqlStoreOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>
    /// Creates a new async.
    /// </summary>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMcpPromptIndexSchemaAsync(_options);
        await SchemaBuilder.CreateMcpResourceIndexSchemaAsync(_options);

        return 1;
    }
}
