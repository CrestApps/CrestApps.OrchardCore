using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.Mcp;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.Mcp.Migrations;

internal sealed class McpConnectionClientsMigrations : DataMigration
{
    private readonly YesSqlStoreOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpConnectionClientsMigrations"/> class.
    /// </summary>
    /// <param name="options">The options.</param>
    public McpConnectionClientsMigrations(IOptions<YesSqlStoreOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>
    /// Creates a new async.
    /// </summary>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMcpConnectionIndexSchemaAsync(_options);

        return 1;
    }
}
