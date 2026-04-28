using System.Data.Common;
using System.Text.Json;
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
    private const int _batchSize = 50;
    private static readonly string _currentDocumentType =
        $"{typeof(AIDataSource).FullName}, {typeof(AIDataSource).Assembly.GetName().Name}";
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
        var yesSqlStoreOptions = serviceProvider.GetRequiredService<IOptions<YesSqlStoreOptions>>().Value;

        var dialect = store.Configuration.SqlDialect;
        var documentTableName = store.Configuration.TableNameConvention.GetDocumentTable(string.Empty);
        var table = $"{store.Configuration.TablePrefix}{documentTableName}";
        var quotedTableName = dialect.QuoteForTableName(table, store.Configuration.Schema);
        var quotedIdColumnName = dialect.QuoteForColumnName(nameof(Document.Id));
        var quotedTypeColumnName = dialect.QuoteForColumnName(nameof(Document.Type));
        var quotedContentColumnName = dialect.QuoteForColumnName(nameof(Document.Content));

        await using var connection = dbConnectionAccessor.CreateConnection();
        await connection.OpenAsync();

        var persistedDataSourceDocuments = await LoadPersistedDataSourceDocumentsAsync(connection, store, yesSqlStoreOptions);

        var command = $"""
            SELECT {quotedIdColumnName}, {quotedContentColumnName}
            FROM {quotedTableName}
            WHERE {quotedTypeColumnName} IN @LegacyDocumentTypes
            """;

        var documents = (await connection.QueryAsync<Document>(command, new
        {
            LegacyDocumentTypes = _legacyDocumentTypes,
        })).ToList();

        if (documents.Count == 0)
        {
            return;
        }

        var importedCount = 0;

        foreach (var batch in BatchDocuments(documents))
        {
            foreach (var document in batch)
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

                    if (persistedDataSourceDocuments.TryGetValue(itemId, out var existingDocumentId))
                    {
                        await UpdatePersistedDataSourceDocumentAsync(
                            connection,
                            store,
                            yesSqlStoreOptions,
                            existingDocumentId,
                            store.Configuration.ContentSerializer.Serialize(dataSource));
                    }
                    else
                    {
                        await dataSourceStore.CreateAsync(dataSource);
                    }

                    importedCount++;
                }
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

        await RebuildPersistedDataSourceIndexAsync(connection, store, yesSqlStoreOptions, logger);
    }

    private static IEnumerable<IReadOnlyList<Document>> BatchDocuments(List<Document> documents)
    {
        for (var index = 0; index < documents.Count; index += _batchSize)
        {
            yield return documents.Skip(index).Take(_batchSize).ToList();
        }
    }

    private static async Task<Dictionary<string, long>> LoadPersistedDataSourceDocumentsAsync(
        DbConnection connection,
        IStore store,
        YesSqlStoreOptions options)
    {
        var dialect = store.Configuration.SqlDialect;
        var documentTableName = store.Configuration.TableNameConvention.GetDocumentTable(options.AICollectionName);
        var table = $"{store.Configuration.TablePrefix}{documentTableName}";
        var quotedTableName = dialect.QuoteForTableName(table, store.Configuration.Schema);
        var quotedIdColumnName = dialect.QuoteForColumnName(nameof(Document.Id));
        var quotedTypeColumnName = dialect.QuoteForColumnName(nameof(Document.Type));
        var quotedContentColumnName = dialect.QuoteForColumnName(nameof(Document.Content));

        var command = $"""
            SELECT {quotedIdColumnName}, {quotedContentColumnName}
            FROM {quotedTableName}
            WHERE {quotedTypeColumnName} = @Type
            """;

        var documents = await connection.QueryAsync<Document>(command, new
        {
            Type = _currentDocumentType,
        });

        var results = new Dictionary<string, long>(StringComparer.Ordinal);

        foreach (var document in documents)
        {
            if (JsonNode.Parse(document.Content) is not JsonObject dataSourceObject)
            {
                continue;
            }

            var itemId = dataSourceObject[nameof(AIDataSource.ItemId)]?.GetValue<string>();

            if (string.IsNullOrWhiteSpace(itemId))
            {
                continue;
            }

            if (!results.TryGetValue(itemId, out var existingDocumentId) || document.Id > existingDocumentId)
            {
                results[itemId] = document.Id;
            }
        }

        return results;
    }

    private static async Task UpdatePersistedDataSourceDocumentAsync(
        DbConnection connection,
        IStore store,
        YesSqlStoreOptions options,
        long documentId,
        string content)
    {
        var dialect = store.Configuration.SqlDialect;
        var documentTableName = store.Configuration.TableNameConvention.GetDocumentTable(options.AICollectionName);
        var table = $"{store.Configuration.TablePrefix}{documentTableName}";
        var quotedTableName = dialect.QuoteForTableName(table, store.Configuration.Schema);
        var quotedIdColumnName = dialect.QuoteForColumnName(nameof(Document.Id));
        var quotedTypeColumnName = dialect.QuoteForColumnName(nameof(Document.Type));
        var quotedContentColumnName = dialect.QuoteForColumnName(nameof(Document.Content));

        await connection.ExecuteAsync(
            $"UPDATE {quotedTableName} SET {quotedTypeColumnName} = @Type, {quotedContentColumnName} = @Content WHERE {quotedIdColumnName} = @Id",
            new
            {
                Id = documentId,
                Type = _currentDocumentType,
                Content = content,
            });
    }

    private static async Task RebuildPersistedDataSourceIndexAsync(
        DbConnection connection,
        IStore store,
        YesSqlStoreOptions options,
        ILogger logger)
    {
        var dialect = store.Configuration.SqlDialect;
        var documentTableName = store.Configuration.TableNameConvention.GetDocumentTable(options.AICollectionName);
        var documentTable = $"{store.Configuration.TablePrefix}{documentTableName}";
        var quotedDocumentTableName = dialect.QuoteForTableName(documentTable, store.Configuration.Schema);
        var quotedDocumentIdColumnName = dialect.QuoteForColumnName(nameof(Document.Id));
        var quotedDocumentTypeColumnName = dialect.QuoteForColumnName(nameof(Document.Type));
        var quotedDocumentContentColumnName = dialect.QuoteForColumnName(nameof(Document.Content));

        var indexTableName = GetCollectionTableName(options.AICollectionName, nameof(AIDataSourceIndex));
        var indexTable = $"{store.Configuration.TablePrefix}{indexTableName}";
        var quotedIndexTableName = dialect.QuoteForTableName(indexTable, store.Configuration.Schema);
        var quotedIndexDocumentIdColumnName = dialect.QuoteForColumnName("DocumentId");
        var quotedIndexItemIdColumnName = dialect.QuoteForColumnName(nameof(AIDataSourceIndex.ItemId));
        var quotedIndexDisplayTextColumnName = dialect.QuoteForColumnName(nameof(AIDataSourceIndex.DisplayText));
        var quotedIndexSourceIndexProfileNameColumnName = dialect.QuoteForColumnName(nameof(AIDataSourceIndex.SourceIndexProfileName));

        var command = $"""
            SELECT {quotedDocumentIdColumnName}, {quotedDocumentContentColumnName}
            FROM {quotedDocumentTableName}
            WHERE {quotedDocumentTypeColumnName} = @Type
            """;

        var documents = await connection.QueryAsync<Document>(command, new
        {
            Type = _currentDocumentType,
        });

        var indexEntries = new Dictionary<string, (long DocumentId, string DisplayText, string SourceIndexProfileName)>(StringComparer.Ordinal);

        foreach (var document in documents)
        {
            if (JsonSerializer.Deserialize<AIDataSource>(document.Content) is not { } dataSource ||
                string.IsNullOrWhiteSpace(dataSource.ItemId))
            {
                continue;
            }

            if (!indexEntries.TryGetValue(dataSource.ItemId, out var existingEntry) || document.Id > existingEntry.DocumentId)
            {
                indexEntries[dataSource.ItemId] = (document.Id, dataSource.DisplayText, dataSource.SourceIndexProfileName);
            }
        }

        await connection.ExecuteAsync($"DELETE FROM {quotedIndexTableName}");

        foreach (var (itemId, entry) in indexEntries)
        {
            await connection.ExecuteAsync(
                $"""
                INSERT INTO {quotedIndexTableName} ({quotedIndexDocumentIdColumnName}, {quotedIndexItemIdColumnName}, {quotedIndexDisplayTextColumnName}, {quotedIndexSourceIndexProfileNameColumnName})
                VALUES (@DocumentId, @ItemId, @DisplayText, @SourceIndexProfileName)
                """,
                new
                {
                    entry.DocumentId,
                    ItemId = itemId,
                    entry.DisplayText,
                    entry.SourceIndexProfileName,
                });
        }

        if (indexEntries.Count > 0 && logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Rebuilt {Count} AI data source index entries from persisted AI documents.",
                indexEntries.Count);
        }
    }

    private static string GetCollectionTableName(string collectionName, string typeName)
        => string.IsNullOrWhiteSpace(collectionName)
            ? typeName
            : $"{collectionName}_{typeName}";

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
