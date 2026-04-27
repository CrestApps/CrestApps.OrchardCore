using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.AI;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.Migrations;

internal sealed class AIProfileIndexMigrations : DataMigration
{
    private readonly YesSqlStoreOptions _option;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileIndexMigrations"/> class.
    /// </summary>
    /// <param name="option">The option.</param>
    public AIProfileIndexMigrations(IOptions<YesSqlStoreOptions> option)
    {
        _option = option.Value;
    }

    /// <summary>
    /// Creates a new async.
    /// </summary>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateAIProfileIndexSchemaAsync(_option);

        return 3;
    }

    /// <summary>
    /// Updates the from1 async.
    /// </summary>
    public static Task<int> UpdateFrom1Async()
    {
        return Task.FromResult(2);
    }

    /// <summary>
    /// Updates the from2 async.
    /// </summary>
    public static Task<int> UpdateFrom2Async()
    {
        return Task.FromResult(3);
    }
}
