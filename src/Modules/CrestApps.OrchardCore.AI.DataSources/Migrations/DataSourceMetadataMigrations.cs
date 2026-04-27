using System.Text.Json.Nodes;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Services;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Scope;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.AI.DataSources.Migrations;

internal sealed class DataSourceMetadataMigrations : DataMigration
{
    private const string LegacyKey = "AIProfileDataSourceMetadata";
    private const string NewKey = nameof(DataSourceMetadata);
    private const int _batchSize = 50;
    private const string _legacyDocumentTypePrefix =
        "CrestApps.OrchardCore.Models.DictionaryDocument`1[[CrestApps.OrchardCore.AI.Models.AIDataSource, CrestApps.OrchardCore.AI.Abstractions";

    private readonly ShellSettings _shellSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="DataSourceMetadataMigrations"/> class.
    /// </summary>
    /// <param name="shellSettings">The shell settings.</param>
    public DataSourceMetadataMigrations(ShellSettings shellSettings)
    {
        _shellSettings = shellSettings;
    }

    /// <summary>
    /// Creates a new .
    /// </summary>
    public int Create()
    {
        if (_shellSettings.IsInitializing())
        {
            return 2;
        }

        ShellScope.AddDeferredTask(scope => MigrateLegacyDataSourcesAsync(scope.ServiceProvider));

        return 2;
    }

    /// <summary>
    /// Updates the from1.
    /// </summary>
    public int UpdateFrom1()
    {
        if (_shellSettings.IsInitializing())
        {
            return 2;
        }

        ShellScope.AddDeferredTask(scope => MigrateLegacyDataSourcesAsync(scope.ServiceProvider));

        return 2;
    }

    private static async Task MigrateLegacyDataSourcesAsync(IServiceProvider serviceProvider)
    {
        await ImportLegacyDataSourcesAsync(serviceProvider);
        await MigrateLegacyProfileMetadataAsync(serviceProvider);
    }

    private static async Task ImportLegacyDataSourcesAsync(IServiceProvider serviceProvider)
    {
        var store = serviceProvider.GetRequiredService<IStore>();
        var dbConnectionAccessor = serviceProvider.GetRequiredService<IDbConnectionAccessor>();
        var dataSourceManager = serviceProvider.GetRequiredService<ICatalogManager<AIDataSource>>();
        var logger = serviceProvider.GetRequiredService<ILogger<DataSourceMetadataMigrations>>();

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
                    if (record.Value is not JsonObject dataSourceObject)
                    {
                        continue;
                    }

                    dataSourceObject[nameof(AIDataSource.ItemId)] ??= record.Key;

                    var itemId = dataSourceObject[nameof(AIDataSource.ItemId)]?.GetValue<string>();
                    if (string.IsNullOrWhiteSpace(itemId))
                    {
                        continue;
                    }

                    var dataSource = await dataSourceManager.FindByIdAsync(itemId);

                    if (dataSource is not null)
                    {
                        await dataSourceManager.UpdateAsync(dataSource, dataSourceObject);
                    }
                    else
                    {
                        dataSource = await dataSourceManager.NewAsync(dataSourceObject);
                        dataSource.ItemId = itemId;
                    }

                    var validationResult = await dataSourceManager.ValidateAsync(dataSource);
                    if (!validationResult.Succeeded)
                    {
                        logger.LogWarning(
                            "Skipping legacy AI data source {ItemId} because validation failed: {Errors}",
                            itemId,
                            string.Join("; ", validationResult.Errors.Select(error => error.ErrorMessage)));
                        continue;
                    }

                    await dataSourceManager.CreateAsync(dataSource);
                    importedCount++;
                }
            }
        }

        if (importedCount > 0 && logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Imported or updated {Count} legacy AI data sources from dictionary documents.",
                importedCount);
        }
    }

    private static async Task MigrateLegacyProfileMetadataAsync(IServiceProvider serviceProvider)
    {
        var profileStore = serviceProvider.GetRequiredService<IAIProfileStore>();

        foreach (var profile in await profileStore.GetAllAsync())
        {
            if (profile.Properties is null ||
                !profile.Properties.ContainsKey(LegacyKey))
            {
                continue;
            }

            var legacyNode = profile.Properties[LegacyKey];
            profile.Properties.Remove(LegacyKey);
            profile.Properties[NewKey] = legacyNode;

            await profileStore.UpdateAsync(profile);
        }
    }

    private static IEnumerable<IReadOnlyList<Document>> BatchDocuments(List<Document> documents)
    {
        for (var index = 0; index < documents.Count; index += _batchSize)
        {
            yield return documents.Skip(index).Take(_batchSize).ToList();
        }
    }
}
