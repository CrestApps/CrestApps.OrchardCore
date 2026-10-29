using CrestApps.Core.Data.YesSql;
using CrestApps.OrchardCore.AI.Core.Indexes;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.Migrations;

/// <summary>
/// Creates the index tables of the AI Quality feature.
/// </summary>
internal sealed class AIQualityIndexMigrations : DataMigration
{
    private readonly YesSqlStoreOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIQualityIndexMigrations"/> class.
    /// </summary>
    /// <param name="options">The YesSql store options, which name the AI collection.</param>
    public AIQualityIndexMigrations(IOptions<YesSqlStoreOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>
    /// Creates the test case and test run tables.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> CreateAsync()
    {
        await AITestIndexSchema.CreateAsync(SchemaBuilder, _options);

        return 1;
    }
}
