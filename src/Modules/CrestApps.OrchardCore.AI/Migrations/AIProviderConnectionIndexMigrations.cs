using System.Text.Json.Nodes;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.AI;
using CrestApps.Core.Services;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Scope;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.AI.Migrations;

public sealed class AIProviderConnectionIndexMigrations : DataMigration
{
    private const int _batchSize = 50;
    private const string _azureConnectionMetadataPropertyName = "AzureConnectionMetadata";
    private const string _legacyAzureConnectionMetadataPropertyName = "AzureOpenAIConnectionMetadata";
    private const string _legacyDocumentTypePrefix =
        "CrestApps.OrchardCore.Models.DictionaryDocument`1[[CrestApps.OrchardCore.AI.Models.AIProviderConnection, CrestApps.OrchardCore.AI.Abstractions";

    private readonly YesSqlStoreOptions _option;

    public AIProviderConnectionIndexMigrations(IOptions<YesSqlStoreOptions> option)
    {
        _option = option.Value;
    }

    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateAIProviderConnectionIndexSchemaAsync(_option);

        ShellScope.AddDeferredTask(scope => ImportLegacyProviderConnectionsAsync(scope.ServiceProvider));

        return 3;
    }

    public static int UpdateFrom1()
    {
        ShellScope.AddDeferredTask(scope => ImportLegacyProviderConnectionsAsync(scope.ServiceProvider));

        return 2;
    }

    public static int UpdateFrom2()
    {
        ShellScope.AddDeferredTask(scope => ImportLegacyProviderConnectionsAsync(scope.ServiceProvider));

        return 3;
    }

    private static async Task ImportLegacyProviderConnectionsAsync(IServiceProvider serviceProvider)
    {
        var store = serviceProvider.GetRequiredService<IStore>();
        var dbConnectionAccessor = serviceProvider.GetRequiredService<IDbConnectionAccessor>();
        var connectionManager = serviceProvider.GetRequiredService<INamedSourceCatalogManager<AIProviderConnection>>();
        var logger = serviceProvider.GetRequiredService<ILogger<AIProviderConnectionIndexMigrations>>();

        var dialect = store.Configuration.SqlDialect;
        var documentTableName = store.Configuration.TableNameConvention.GetDocumentTable(string.Empty);
        var table = $"{store.Configuration.TablePrefix}{documentTableName}";
        var quotedTableName = dialect.QuoteForTableName(table, store.Configuration.Schema);
        var quotedIdColumnName = dialect.QuoteForColumnName(nameof(Document.Id));
        var quotedTypeColumnName = dialect.QuoteForColumnName(nameof(Document.Type));
        var quotedContentColumnName = dialect.QuoteForColumnName(nameof(Document.Content));

        await using var connection = dbConnectionAccessor.CreateConnection();
        await connection.OpenAsync();

        var sqlBuilder = new SqlBuilder(store.Configuration.TablePrefix, store.Configuration.SqlDialect);
        sqlBuilder.AddSelector(quotedIdColumnName);
        sqlBuilder.AddSelector("," + quotedContentColumnName);
        sqlBuilder.From(quotedTableName);
        sqlBuilder.WhereAnd($" {quotedTypeColumnName} LIKE '{_legacyDocumentTypePrefix}%' ");

        var documents = (await connection.QueryAsync<Document>(sqlBuilder.ToSqlString())).ToList();

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
                    if (record.Value is not JsonObject connectionObject)
                    {
                        continue;
                    }

                    NormalizeLegacyConnectionProperties(connectionObject);

                    connectionObject[nameof(AIProviderConnection.ItemId)] ??= record.Key;

                    var itemId = connectionObject[nameof(AIProviderConnection.ItemId)]?.GetValue<string>();
                    if (string.IsNullOrWhiteSpace(itemId))
                    {
                        continue;
                    }

                    var sourceName = connectionObject[nameof(AIProviderConnection.Source)]?.GetValue<string>()
                        ?? connectionObject[nameof(AIProviderConnection.ClientName)]?.GetValue<string>();
                    var name = connectionObject[nameof(AIProviderConnection.Name)]?.GetValue<string>()?.Trim();

                    AIProviderConnection providerConnection = await connectionManager.FindByIdAsync(itemId);

                    if (providerConnection is null &&
                        !string.IsNullOrWhiteSpace(name) &&
                        !string.IsNullOrWhiteSpace(sourceName))
                    {
                        providerConnection = await connectionManager.GetAsync(name, sourceName);
                    }

                    if (providerConnection is not null)
                    {
                        await connectionManager.UpdateAsync(providerConnection, connectionObject);
                    }
                    else
                    {
                        if (string.IsNullOrWhiteSpace(sourceName))
                        {
                            logger.LogWarning(
                                "Skipping legacy AI provider connection {ItemId} because no source/client name was found.",
                                itemId);
                            continue;
                        }

                        providerConnection = await connectionManager.NewAsync(sourceName, connectionObject);
                        providerConnection.ItemId = itemId;
                    }

                    var validationResult = await connectionManager.ValidateAsync(providerConnection);
                    if (!validationResult.Succeeded)
                    {
                        logger.LogWarning(
                            "Skipping legacy AI provider connection {ItemId} because validation failed: {Errors}",
                            itemId,
                            string.Join("; ", validationResult.Errors.Select(error => error.ErrorMessage)));
                        continue;
                    }

                    await connectionManager.CreateAsync(providerConnection);
                    importedCount++;
                }
            }
        }

        if (importedCount > 0 && logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Imported or updated {Count} legacy AI provider connections from dictionary documents.",
                importedCount);
        }
    }

    private static IEnumerable<IReadOnlyList<Document>> BatchDocuments(List<Document> documents)
    {
        for (var index = 0; index < documents.Count; index += _batchSize)
        {
            yield return documents.Skip(index).Take(_batchSize).ToList();
        }
    }

    private static void NormalizeLegacyConnectionProperties(JsonObject connectionObject)
    {
        if (connectionObject[nameof(AIProviderConnection.Properties)] is not JsonObject propertiesObject ||
            propertiesObject[_azureConnectionMetadataPropertyName] is JsonObject ||
            propertiesObject[_legacyAzureConnectionMetadataPropertyName] is not JsonObject legacyMetadata)
        {
            return;
        }

        propertiesObject[_azureConnectionMetadataPropertyName] = legacyMetadata.DeepClone();
    }
}
