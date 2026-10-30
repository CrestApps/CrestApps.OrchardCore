using CrestApps.Core.Data.YesSql;
using CrestApps.OrchardCore.AI.Core.ApiKeys;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.Migrations;

/// <summary>
/// Creates the API key index table.
/// </summary>
internal sealed class AIApiKeyIndexMigrations : DataMigration
{
    private readonly YesSqlStoreOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIApiKeyIndexMigrations"/> class.
    /// </summary>
    /// <param name="options">The YesSql store options, which name the AI collection.</param>
    public AIApiKeyIndexMigrations(IOptions<YesSqlStoreOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>
    /// Creates the table.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> CreateAsync()
    {
        await AIApiKeyIndexSchema.CreateAsync(SchemaBuilder, _options);

        return 1;
    }
}
