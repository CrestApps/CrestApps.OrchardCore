using System.Data.Common;
using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.FileSources;
using CrestApps.Core.Data.YesSql.Indexes.WebCrawlers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;
using YesSql;

namespace CrestApps.OrchardCore.AI.WebCrawlers.Migrations;

/// <summary>
/// Creates the YesSql index tables backing the web-crawler stores.
/// </summary>
/// <remarks>
/// These tables belong to this feature alone. They are still created tolerantly because a tenant set up
/// under an earlier build has them already: File Sources used to store its records in the same index and
/// created them itself. Creating a table that is already there is a failure, not a no-op, so enabling this
/// feature on such a tenant would otherwise log an error and abandon the rest of its migration.
/// </remarks>
internal sealed class WebCrawlerIndexMigrations : DataMigration
{
    private readonly YesSqlStoreOptions _option;
    private readonly IStore _store;
    private readonly ILogger<WebCrawlerIndexMigrations> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebCrawlerIndexMigrations"/> class.
    /// </summary>
    /// <param name="option">The YesSql store options.</param>
    /// <param name="store">The YesSql store, which carries the table naming and the dialect.</param>
    /// <param name="logger">The logger.</param>
    public WebCrawlerIndexMigrations(
        IOptions<YesSqlStoreOptions> option,
        IStore store,
        ILogger<WebCrawlerIndexMigrations> logger
        )
    {
        _option = option.Value;
        _store = store;
        _logger = logger;
    }

    /// <summary>
    /// Creates the web crawler, web crawl-state and ingestion item-state index schemas.
    /// </summary>
    public async Task<int> CreateAsync()
    {
        try
        {
            await SchemaBuilder.CreateWebCrawlerIndexSchemaAsync(_option);
            await SchemaBuilder.CreateWebCrawlStateIndexSchemaAsync(_option);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create web crawler index schemas. It may be already exists.. you can ignore this warning.");
        }

        await EnsureIngestionItemStateIndexAsync();

        return 2;
    }

    /// <summary>
    /// Creates the ingestion item-state index on a tenant that enabled this feature before it needed one.
    /// </summary>
    public async Task<int> UpdateFrom1Async()
    {
        await EnsureIngestionItemStateIndexAsync();

        return 2;
    }

    // A crawler that feeds a File data source keeps its progress as per-item state, and the store for it is now
    // registered with the crawler stores rather than only with File Sources. The table is shared with File
    // Sources, which creates it too, so it is created only when missing: a create that fails on an existing
    // table would abort the whole migration transaction on PostgreSQL.
    private async Task EnsureIngestionItemStateIndexAsync()
    {
        if (await IngestionItemStateIndexExistsAsync())
        {
            return;
        }

        await SchemaBuilder.CreateIngestionItemStateIndexSchemaAsync(_option);
    }

    private async Task<bool> IngestionItemStateIndexExistsAsync()
    {
        var configuration = _store.Configuration;
        var tableName = configuration.TablePrefix +
            configuration.TableNameConvention.GetIndexTable(typeof(IngestionItemStateIndex), _option.AICollectionName);

        // Probed on the migration's own transaction: a second connection deadlocks on SQLite, and this one also
        // sees a table an earlier step of the same run created.
        var transaction = SchemaBuilder.Transaction;

        await using var command = transaction.Connection.CreateCommand();
        command.Transaction = transaction;

        if (string.Equals(configuration.SqlDialect.Name, "Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            command.CommandText = "SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = @TableName";
        }
        else if (string.IsNullOrEmpty(configuration.Schema))
        {
            command.CommandText = "SELECT COUNT(1) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @TableName";
        }
        else
        {
            command.CommandText = "SELECT COUNT(1) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = @Schema AND TABLE_NAME = @TableName";
            AddParameter(command, "@Schema", configuration.Schema);
        }

        AddParameter(command, "@TableName", tableName);

        return Convert.ToInt64(await command.ExecuteScalarAsync()) > 0;
    }

    private static void AddParameter(DbCommand command, string name, string value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
