using CrestApps.Core.Data.YesSql;
using CrestApps.OrchardCore.AI.Core.Integrations;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.Migrations;

/// <summary>
/// Creates the integration connection index table.
/// </summary>
internal sealed class AIIntegrationIndexMigrations : DataMigration
{
    private readonly YesSqlStoreOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIIntegrationIndexMigrations"/> class.
    /// </summary>
    /// <param name="options">The YesSql store options, which name the AI collection.</param>
    public AIIntegrationIndexMigrations(IOptions<YesSqlStoreOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>
    /// Creates the table.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> CreateAsync()
    {
        await AIIntegrationConnectionIndexSchema.CreateAsync(SchemaBuilder, _options);

        return 1;
    }
}
