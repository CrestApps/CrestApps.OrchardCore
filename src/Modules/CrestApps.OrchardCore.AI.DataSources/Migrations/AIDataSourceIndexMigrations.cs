using System.Text.Json.Nodes;
using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.DataSources;
using CrestApps.Core.Services;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Scope;
using YesSql;

namespace CrestApps.OrchardCore.AI.DataSources.Migrations;

internal sealed class AIDataSourceIndexMigrations : DataMigration
{
    private static readonly string[] _legacyDocumentTypes =
    [
        "CrestApps.OrchardCore.Models.DictionaryDocument`1[[CrestApps.AI.Models.AIDataSource, CrestApps.AI.Abstractions, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null]], CrestApps.OrchardCore.Abstractions",
        "CrestApps.OrchardCore.Models.DictionaryDocument`1[[CrestApps.Core.AI.Models.AIDataSource, CrestApps.Core.AI.Abstractions, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]], CrestApps.OrchardCore.Abstractions",
    ];

    private readonly YesSqlStoreOptions _option;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIDataSourceIndexMigrations"/> class.
    /// </summary>
    /// <param name="option">The YesSql store options.</param>
    public AIDataSourceIndexMigrations(IOptions<YesSqlStoreOptions> option)
    {
        _option = option.Value;
    }

    /// <summary>
    /// Creates the AI data source index schema and imports any legacy AI data source records.
    /// </summary>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateAIDataSourceIndexSchemaAsync(_option);

        ShellScope.AddDeferredTask(scope => ImportLegacyDataSourcesAsync(scope.ServiceProvider));

        return 1;
    }

    private static async Task ImportLegacyDataSourcesAsync(IServiceProvider serviceProvider)
    {
        var store = serviceProvider.GetRequiredService<IStore>();
        var dbConnectionAccessor = serviceProvider.GetRequiredService<IDbConnectionAccessor>();
        var dataSourceStore = serviceProvider.GetRequiredService<IAIDataSourceStore>();
        var dataSourceManager = serviceProvider.GetRequiredService<ICatalogManager<AIDataSource>>();
        var logger = serviceProvider.GetRequiredService<ILogger<AIDataSourceIndexMigrations>>();
        var existingDataSources = (await dataSourceStore.GetAllAsync()).ToDictionary(dataSource => dataSource.ItemId, StringComparer.Ordinal);

        var dialect = store.Configuration.SqlDialect;
        var documentTableName = store.Configuration.TableNameConvention.GetDocumentTable(string.Empty);
        var table = $"{store.Configuration.TablePrefix}{documentTableName}";
        var quotedTableName = dialect.QuoteForTableName(table, store.Configuration.Schema);
        var quotedIdColumnName = dialect.QuoteForColumnName(nameof(Document.Id));
        var quotedTypeColumnName = dialect.QuoteForColumnName(nameof(Document.Type));
        var quotedContentColumnName = dialect.QuoteForColumnName(nameof(Document.Content));

        await using var connection = dbConnectionAccessor.CreateConnection();
        await connection.OpenAsync();

        var command = $"""
            SELECT {quotedIdColumnName}, {quotedContentColumnName}
            FROM {quotedTableName}
            WHERE {quotedTypeColumnName} IN @LegacyDocumentTypes
            """;

        var documents = await connection.QueryAsync<Document>(command, new
        {
            LegacyDocumentTypes = _legacyDocumentTypes,
        });

        if (!documents.Any())
        {
            return;
        }

        var importedCount = 0;


        foreach (var document in documents)
        {
            if (JsonNode.Parse(document.Content)?["Records"] is not JsonObject recordsObject)
            {
                continue;
            }

            foreach (var record in recordsObject)
            {
                if (record.Value is not JsonObject dataSourceObject)
                {
                    continue;
                }

                MigrateLegacySourceIndexProfileName(dataSourceObject);
                dataSourceObject[nameof(AIDataSource.ItemId)] ??= record.Key;

                var itemId = dataSourceObject[nameof(AIDataSource.ItemId)]?.GetValue<string>();

                if (string.IsNullOrWhiteSpace(itemId))
                {
                    continue;
                }

                var dataSource = await dataSourceManager.NewAsync(dataSourceObject);
                dataSource.ItemId = itemId;

                var validationResult = await dataSourceManager.ValidateAsync(dataSource);

                if (!validationResult.Succeeded)
                {
                    logger.LogWarning(
                        "Skipping legacy AI data source {ItemId} because validation failed: {Errors}",
                        itemId,
                        string.Join("; ", validationResult.Errors.Select(error => error.ErrorMessage)));

                    continue;
                }

                if (existingDataSources.TryGetValue(itemId, out var existingDataSource))
                {
                    await dataSourceManager.UpdateAsync(existingDataSource, dataSourceObject);
                }
                else
                {
                    await dataSourceStore.CreateAsync(dataSource);
                    existingDataSources[itemId] = dataSource;
                }

                importedCount++;
            }
        }

        if (importedCount > 0)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(
                    "Imported or updated {Count} legacy AI data sources into the indexed store.",
                    importedCount);
            }

            var session = serviceProvider.GetRequiredService<ISession>();
            await session.SaveChangesAsync();
        }
    }

    private static void MigrateLegacySourceIndexProfileName(JsonObject dataSourceObject)
    {
        if (dataSourceObject[nameof(AIDataSource.SourceIndexProfileName)]?.GetValue<string>() is { Length: > 0 })
        {
            return;
        }

        if (dataSourceObject["ProfileSource"]?.GetValue<string>() is not { Length: > 0 } profileSource)
        {
            return;
        }

        dataSourceObject[nameof(AIDataSource.SourceIndexProfileName)] = profileSource;
    }
}
