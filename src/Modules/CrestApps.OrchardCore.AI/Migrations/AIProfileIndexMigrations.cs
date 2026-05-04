using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.AI.Migrations;

internal sealed class AIProfileIndexMigrations : DataMigration
{
    private readonly YesSqlStoreOptions _option;
    private readonly ILogger<AIProfileIndexMigrations> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileIndexMigrations"/> class.
    /// </summary>
    /// <param name="option">The option.</param>
    public AIProfileIndexMigrations(
        IOptions<YesSqlStoreOptions> option,
        ILogger<AIProfileIndexMigrations> logger)
    {
        _option = option.Value;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new async.
    /// </summary>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateAIProfileIndexSchemaAsync(_option);

        return 4;
    }

    /// <summary>
    /// Updates the from1 async.
    /// </summary>
    public static Task<int> UpdateFrom1Async()
    {
        return Task.FromResult(4);
    }

    /// <summary>
    /// Updates the from2 async.
    /// </summary>
    public static Task<int> UpdateFrom2Async()
    {
        return Task.FromResult(4);
    }

    /// <summary>
    /// Updates the from3 async.
    /// </summary>
    public async Task<int> UpdateFrom3Async()
    {
        try
        {
            await SchemaBuilder.AlterIndexTableAsync<AIProfileIndex>(table =>
            {
                table.AddColumn<string>("Type", column => column.WithLength(50));
            }, collection: _option.AICollectionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while updating from version 3. It's probably that the columns exists. You can ignore it.");
        }

        return 4;
    }
}
