using CrestApps.Core.AI.Models;
using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.AIChat;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.BackgroundJobs;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Scope;
using YesSql;
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

        return 6;
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

    /// <summary>
    /// Adds the <c>ClientId</c> column CrestApps.Core preview.375 writes for every chat session, so a site created
    /// before it can save sessions again, then fills it for the anonymous sessions already stored.
    /// </summary>
    /// <remarks>
    /// The column is added in the migration's own transaction. The backfill re-saves documents and commits, which
    /// must not happen inside the migration (a commit there detaches the migration record and half-commits the
    /// feature's other steps), so it runs after the request in its own scope. If it cannot run, only the visitor
    /// lookup of sessions stored before the upgrade is affected; saving works either way.
    /// </remarks>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom5Async()
    {
        await SchemaBuilder.AddAIChatSessionIndexClientIdColumnAsync(_option);

        if (ShellScope.Current is not null)
        {
            await HttpBackgroundJob.ExecuteAfterEndOfRequestAsync("ai-chat-session-client-id-backfill", static scope =>
                BackfillClientIdsAsync(
                    scope.ServiceProvider.GetRequiredService<ISession>(),
                    scope.ServiceProvider.GetRequiredService<IOptions<YesSqlStoreOptions>>().Value,
                    scope.ServiceProvider.GetRequiredService<ILogger<AIChatSessionIndexMigrations>>()));
        }

        return 6;
    }

    /// <summary>
    /// Writes the client id of every stored anonymous chat session into its index row.
    /// </summary>
    /// <param name="session">The session the documents are re-saved through.</param>
    /// <param name="options">The store options that name the AI collection.</param>
    /// <param name="logger">The logger.</param>
    internal static async Task BackfillClientIdsAsync(ISession session, YesSqlStoreOptions options, ILogger logger)
    {
        try
        {
            var count = await session.BackfillAIChatSessionIndexClientIdAsync(options);

            if (count > 0 && logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Filled the client id of {Count} stored anonymous AI chat sessions.", count);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fill the client id of the stored anonymous AI chat sessions.");
        }
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
