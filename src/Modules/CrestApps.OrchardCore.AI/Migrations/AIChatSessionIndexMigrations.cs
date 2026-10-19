using CrestApps.Core.AI.Models;
using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.AIChat;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.AI.Migrations;

internal sealed class AIChatSessionIndexMigrations : DataMigration
{
    private readonly YesSqlStoreOptions _option;
    private readonly ILogger<AIChatSessionIndexMigrations> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIChatSessionIndexMigrations"/> class.
    /// </summary>
    /// <param name="option">The option.</param>
    /// <param name="logger">The logger.</param>
    public AIChatSessionIndexMigrations(
        IOptions<YesSqlStoreOptions> option,
        ILogger<AIChatSessionIndexMigrations> logger)
    {
        _option = option.Value;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new async.
    /// </summary>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateAIChatSessionIndexSchemaAsync(_option);

        await CreateReportIndexAsync();

        return 5;
    }

    /// <summary>
    /// Updates the from1 async.
    /// </summary>
    public async Task<int> UpdateFrom1Async()
    {
        await BackwardCompatibleMigrationStep();

        return 4;
    }

    /// <summary>
    /// Updates the from1 async.
    /// </summary>
    public async Task<int> UpdateFrom2Async()
    {
        await BackwardCompatibleMigrationStep();

        return 4;
    }

    /// <summary>
    /// Updates the from1 async.
    /// </summary>
    public async Task<int> UpdateFrom3Async()
    {
        await BackwardCompatibleMigrationStep();

        return 4;
    }

    private async Task BackwardCompatibleMigrationStep()
    {
        try
        {
            await SchemaBuilder.AlterIndexTableAsync<AIChatSessionIndex>(table =>
            {
                table.AddColumn<ChatSessionStatus>("Status");
                table.AddColumn<DateTime>("LastActivityUtc");
            }, collection: _option.AICollectionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add the Status and LastActivityUtc columns to the AI chat session index table. It's probably that the columns exists. You can ignore it.");
        }
    }

    /// <summary>
    /// Adds the index reports read the records through, newest first by <c>LastActivityUtc</c> a page at a time, so
    /// each page and each date range is found by seeking.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom4Async()
    {
        await CreateReportIndexAsync();

        return 5;
    }

    private Task<bool> CreateReportIndexAsync()
    {
        return IndexTableIndexCreation.CreateIfMissingAsync<AIChatSessionIndex>(
            SchemaBuilder,
            "IDX_AIChatSessionIndex_Report",
            _option.AICollectionName,
            "LastActivityUtc", "Id");
    }
}
