using CrestApps.Core.Data.YesSql;
using CrestApps.OrchardCore.AI.Core.Indexes;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.Migrations;

/// <summary>
/// Creates the table an AI profile's saved versions are listed from.
/// </summary>
internal sealed class AIProfileVersionIndexMigrations : DataMigration
{
    private readonly YesSqlStoreOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileVersionIndexMigrations"/> class.
    /// </summary>
    /// <param name="options">The store options that name the AI collection.</param>
    public AIProfileVersionIndexMigrations(IOptions<YesSqlStoreOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>
    /// Creates the index table.
    /// </summary>
    public async Task<int> CreateAsync()
    {
        await AIProfileVersionIndexSchema.CreateAsync(SchemaBuilder, _options);

        return 1;
    }
}
