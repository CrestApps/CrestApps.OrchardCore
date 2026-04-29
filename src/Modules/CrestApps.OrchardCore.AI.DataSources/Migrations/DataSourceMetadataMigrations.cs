using System.Text.Json.Nodes;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Infrastructure;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;
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
    private const string _knowledgeBaseIndexUniqueName = "AI Knowledge Base Warehouse";

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
            return 3;
        }

        ShellScope.AddDeferredTask(scope => MigrateLegacyDataSourcesAsync(scope.ServiceProvider));

        return 3;
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

    /// <summary>
    /// Updates the from2.
    /// </summary>
    public int UpdateFrom2()
    {
        if (_shellSettings.IsInitializing())
        {
            return 3;
        }

        ShellScope.AddDeferredTask(scope => MigrateLegacyDataSourcesAsync(scope.ServiceProvider));

        return 3;
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
        var indexProfileStore = serviceProvider.GetRequiredService<IIndexProfileStore>();
        var dataSourceOptions = serviceProvider.GetRequiredService<IOptions<AIDataSourceOptions>>().Value;
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

                await PopulateIndexConfigurationAsync(
                    serviceProvider,
                    indexProfileStore,
                    dataSourceOptions,
                    dataSource,
                    logger);

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

    private static async Task PopulateIndexConfigurationAsync(
        IServiceProvider serviceProvider,
        IIndexProfileStore indexProfileStore,
        AIDataSourceOptions dataSourceOptions,
        AIDataSource dataSource,
        ILogger logger)
    {
        if (!string.IsNullOrWhiteSpace(dataSource.SourceIndexProfileName))
        {
            var sourceProfile = await indexProfileStore.FindByNameAsync(dataSource.SourceIndexProfileName);

            if (sourceProfile != null)
            {
                var masterProfiles = await indexProfileStore.GetByTypeAsync(DataSourceConstants.IndexingTaskType);

                if (TryPopulateIndexConfiguration(dataSource, sourceProfile, masterProfiles, dataSourceOptions))
                {
                    return;
                }

                if (string.IsNullOrWhiteSpace(dataSource.AIKnowledgeBaseIndexProfileName))
                {
                    var knowledgeBaseIndex = await GetOrCreateKnowledgeBaseIndexAsync(
                        serviceProvider,
                        sourceProfile.ProviderName,
                        logger);

                    if (knowledgeBaseIndex != null)
                    {
                        dataSource.AIKnowledgeBaseIndexProfileName = knowledgeBaseIndex.Name;
                        TryPopulateFieldMapping(dataSource, sourceProfile, dataSourceOptions);
                    }
                }
            }
        }
    }

    private static bool TryPopulateIndexConfiguration(
        AIDataSource dataSource,
        IndexProfile sourceProfile,
        IEnumerable<IndexProfile> masterProfiles,
        AIDataSourceOptions dataSourceOptions)
    {
        if (string.IsNullOrWhiteSpace(dataSource.AIKnowledgeBaseIndexProfileName))
        {
            var knowledgeBaseIndex = masterProfiles
                .FirstOrDefault(profile => string.Equals(profile.ProviderName, sourceProfile.ProviderName, StringComparison.OrdinalIgnoreCase))
                ?? masterProfiles.FirstOrDefault();

            if (knowledgeBaseIndex != null)
            {
                dataSource.AIKnowledgeBaseIndexProfileName = knowledgeBaseIndex.Name;
            }
        }

        return TryPopulateFieldMapping(dataSource, sourceProfile, dataSourceOptions);
    }

    private static bool TryPopulateFieldMapping(
        AIDataSource dataSource,
        IndexProfile sourceProfile,
        AIDataSourceOptions dataSourceOptions)
    {
        if (dataSourceOptions.GetFieldMapping(sourceProfile.ProviderName, sourceProfile.Type) is not DataSourceFieldMapping fieldMapping)
        {
            return !string.IsNullOrWhiteSpace(dataSource.ContentFieldName);
        }

        dataSource.KeyFieldName ??= fieldMapping.DefaultKeyField;
        dataSource.TitleFieldName ??= fieldMapping.DefaultTitleField;
        dataSource.ContentFieldName ??= fieldMapping.DefaultContentField;

        return !string.IsNullOrWhiteSpace(dataSource.ContentFieldName);
    }

    private static async Task<IndexProfile> GetOrCreateKnowledgeBaseIndexAsync(
        IServiceProvider serviceProvider,
        string providerName,
        ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            return null;
        }

        var indexProfileManager = serviceProvider.GetService<IIndexProfileManager>();
        if (indexProfileManager is null)
        {
            return null;
        }

        var knowledgeBaseIndex = await indexProfileManager.FindByNameAsync(_knowledgeBaseIndexUniqueName);
        if (knowledgeBaseIndex != null)
        {
            return knowledgeBaseIndex;
        }

        var indexManager = serviceProvider.GetKeyedService<IIndexManager>(providerName);
        if (indexManager is null)
        {
            logger.LogWarning(
                "Unable to create the AI knowledge base index profile for provider '{ProviderName}' because the index manager is missing.",
                providerName);

            return null;
        }

        knowledgeBaseIndex = await indexProfileManager.NewAsync(providerName, DataSourceConstants.IndexingTaskType, new JsonObject
        {
            { "Name", _knowledgeBaseIndexUniqueName },
            { "IndexName", "AIKnowledgeBaseWarehouse" },
        });

        knowledgeBaseIndex.Put(await FindFirstEmbeddingMetadataAsync(serviceProvider, logger));
        await indexProfileManager.CreateAsync(knowledgeBaseIndex);

        if (!await indexManager.ExistsAsync(knowledgeBaseIndex.IndexFullName) &&
            !await indexManager.CreateAsync(knowledgeBaseIndex))
        {
            logger.LogWarning("Unable to create the '{IndexName}' knowledge base index in provider '{ProviderName}'.", _knowledgeBaseIndexUniqueName, providerName);

            await indexProfileManager.DeleteAsync(knowledgeBaseIndex);

            return null;
        }

        return knowledgeBaseIndex;
    }

    private static async Task<DataSourceIndexProfileMetadata> FindFirstEmbeddingMetadataAsync(IServiceProvider serviceProvider, ILogger logger)
    {
        var deploymentManager = serviceProvider.GetRequiredService<IAIDeploymentManager>();
        var deployment = (await deploymentManager.GetByTypeAsync(AIDeploymentType.Embedding)).FirstOrDefault();

        if (deployment != null)
        {
            var metadata = new DataSourceIndexProfileMetadata();
            metadata.SetEmbeddingDeploymentName(deployment.Name);

            return metadata;
        }

        logger.LogWarning(
            "No embedding deployment was found. " +
            "The 'AI Knowledge Base Warehouse' index was created without an embedding deployment. " +
            "To enable knowledge base indexing, configure an embedding deployment, " +
            "then update the 'AI Knowledge Base Warehouse' index in Search > Indexing to set an embedding deployment.");

        return new DataSourceIndexProfileMetadata();
    }
}
