using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.A2A;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.A2A.Migrations;

internal sealed class A2AConnectionMigrations : DataMigration
{
    private readonly YesSqlStoreOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="A2AConnectionMigrations"/> class.
    /// </summary>
    /// <param name="options">The options.</param>
    public A2AConnectionMigrations(IOptions<YesSqlStoreOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>
    /// Creates a new async.
    /// </summary>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateA2AConnectionIndexSchemaAsync(_options);

        return 1;
    }
}
