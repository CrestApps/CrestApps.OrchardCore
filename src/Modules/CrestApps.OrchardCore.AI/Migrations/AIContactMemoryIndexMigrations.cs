using CrestApps.Core.Data.YesSql;
using CrestApps.OrchardCore.AI.Core.Indexes;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.Migrations;

/// <summary>
/// Creates the table contact memory notes are found through.
/// </summary>
internal sealed class AIContactMemoryIndexMigrations : DataMigration
{
    private readonly YesSqlStoreOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIContactMemoryIndexMigrations"/> class.
    /// </summary>
    /// <param name="options">The store options that name the AI collection.</param>
    public AIContactMemoryIndexMigrations(IOptions<YesSqlStoreOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>
    /// Creates the index table.
    /// </summary>
    public async Task<int> CreateAsync()
    {
        await AIContactMemoryIndexSchema.CreateAsync(SchemaBuilder, _options);

        return 1;
    }
}
