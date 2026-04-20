using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI.Documents.Models;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Core;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Scope;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.AI.Migrations;

/// <summary>
/// Imports AI profiles directly from v1 dictionary documents without changing the
/// preview-era AI profile migration path that some tenants already executed.
/// </summary>
internal sealed class AIProfileV1DocumentMigrations : DataMigration
{
    private const int _batchSize = 50;
    private const string _legacyFunctionInvocationMetadataKey = "AIProfileFunctionInvocationMetadata";
    private const string _legacyDataSourceMetadataKey = "AIProfileDataSourceMetadata";
    private const string _legacyDocumentsMetadataKey = "AIProfileDocumentsMetadata";
    private const string _legacyAnalyticsMetadataKey = "AIProfileAnalyticsMetadata";
    private const string _legacyAzureRagMetadataKey = "AzureRagChatMetadata";
    private const string _functionInvocationMetadataKey = nameof(FunctionInvocationMetadata);
    private const string _dataSourceMetadataKey = "DataSourceMetadata";
    private const string _documentsMetadataKey = nameof(DocumentsMetadata);
    private const string _analyticsMetadataKey = nameof(AnalyticsMetadata);
    private const string _dataSourceRagMetadataKey = nameof(AIDataSourceRagMetadata);
    private const string _legacyDictionaryDocumentTypePrefix =
        "CrestApps.OrchardCore.Models.DictionaryDocument`1[[CrestApps.OrchardCore.AI.Models.AIProfile, CrestApps.OrchardCore.AI.Abstractions, Version=1.";
    private const string _legacyProfileTypePrefix = "CrestApps.OrchardCore.AI.Models.AIProfile,";
    private const string _currentProfileTypePrefix = "CrestApps.Core.AI.Models.AIProfile,";

    public static int Create()
    {
        ShellScope.AddDeferredTask(scope => ImportAndNormalizeProfilesAsync(scope.ServiceProvider));

        return 1;
    }

    private static async Task ImportAndNormalizeProfilesAsync(IServiceProvider serviceProvider)
    {
        await ImportLegacyProfilesAsync(serviceProvider);
        await NormalizePersistedProfilesAsync(serviceProvider);
    }

    private static async Task ImportLegacyProfilesAsync(IServiceProvider serviceProvider)
    {
        var store = serviceProvider.GetRequiredService<IStore>();
        var dbConnectionAccessor = serviceProvider.GetRequiredService<IDbConnectionAccessor>();
        var profileManager = serviceProvider.GetRequiredService<IAIProfileManager>();
        var logger = serviceProvider.GetRequiredService<ILogger<AIProfileV1DocumentMigrations>>();

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
        sqlBuilder.WhereAnd($" {quotedTypeColumnName} LIKE '{_legacyDictionaryDocumentTypePrefix}%' ");

        var documents = (await connection.QueryAsync<Document>(sqlBuilder.ToSqlString())).ToList();

        if (documents.Count == 0)
        {
            return;
        }

        var totalMigrated = 0;

        foreach (var batch in BatchDocuments(documents))
        {
            foreach (var document in batch)
            {
                var jsonObject = JsonNode.Parse(document.Content);

                if (jsonObject?["Records"] is not JsonObject recordsObject || recordsObject.Count == 0)
                {
                    continue;
                }

                foreach (var record in recordsObject)
                {
                    if (record.Value is not JsonObject profileObject)
                    {
                        continue;
                    }

                    try
                    {
                        NormalizeLegacyProfileObject(profileObject, record.Key);

                        var itemId = profileObject[nameof(AIProfile.ItemId)]?.GetValue<string>();
                        var name = profileObject[nameof(AIProfile.Name)]?.GetValue<string>()?.Trim();
                        AIProfile profile = null;

                        if (!string.IsNullOrWhiteSpace(itemId))
                        {
                            profile = await profileManager.FindByIdAsync(itemId);
                        }

                        if (profile is null && !string.IsNullOrWhiteSpace(name))
                        {
                            profile = await profileManager.FindByNameAsync(name);
                        }

                        if (profile is not null)
                        {
                            await profileManager.UpdateAsync(profile, profileObject);
                        }
                        else
                        {
                            profile = await profileManager.NewAsync(profileObject);

                            if (!string.IsNullOrWhiteSpace(itemId) && UniqueId.IsValid(itemId))
                            {
                                profile.ItemId = itemId;
                            }
                        }

                        var validationResult = await profileManager.ValidateAsync(profile);

                        if (!validationResult.Succeeded)
                        {
                            logger.LogWarning(
                                "Skipping v1 AI profile {ItemId} because validation failed: {Errors}",
                                itemId ?? name ?? record.Key,
                                string.Join("; ", validationResult.Errors.Select(error => error.ErrorMessage)));
                            continue;
                        }

                        await profileManager.CreateAsync(profile);
                        totalMigrated++;
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to migrate v1 AIProfile with key '{Key}'. Skipping.", record.Key);
                    }
                }
            }
        }

        if (totalMigrated > 0 && logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Completed v1 AIProfile migration. Migrated or updated {Count} profiles.", totalMigrated);
        }
    }

    private static async Task NormalizePersistedProfilesAsync(IServiceProvider serviceProvider)
    {
        var store = serviceProvider.GetRequiredService<IStore>();
        var dbConnectionAccessor = serviceProvider.GetRequiredService<IDbConnectionAccessor>();
        var logger = serviceProvider.GetRequiredService<ILogger<AIProfileV1DocumentMigrations>>();

        var dialect = store.Configuration.SqlDialect;
        var documentTableName = store.Configuration.TableNameConvention.GetDocumentTable(AIConstants.AICollectionName);
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
        sqlBuilder.WhereAnd(
            $" ({quotedTypeColumnName} LIKE '{_legacyProfileTypePrefix}%' OR {quotedTypeColumnName} LIKE '{_currentProfileTypePrefix}%') ");

        var documents = (await connection.QueryAsync<Document>(sqlBuilder.ToSqlString())).ToList();

        if (documents.Count == 0)
        {
            return;
        }

        var totalUpdated = 0;

        foreach (var document in documents)
        {
            if (JsonNode.Parse(document.Content) is not JsonObject profileDocument)
            {
                continue;
            }

            if (!NormalizePersistedProfileDocument(profileDocument))
            {
                continue;
            }

            await connection.ExecuteAsync(
                $"UPDATE {quotedTableName} SET {quotedContentColumnName} = @Content WHERE {quotedIdColumnName} = @Id",
                new
                {
                    document.Id,
                    Content = profileDocument.ToJsonString(),
                });

            totalUpdated++;
        }

        if (totalUpdated > 0 && logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Normalized {Count} imported v1 AI profiles in {TableName} to the current property structure.",
                totalUpdated,
                table);
        }
    }

    private static IEnumerable<IReadOnlyList<Document>> BatchDocuments(List<Document> documents)
    {
        for (var index = 0; index < documents.Count; index += _batchSize)
        {
            yield return documents.Skip(index).Take(_batchSize).ToList();
        }
    }

    private static void NormalizeLegacyProfileObject(JsonObject profileObject, string fallbackItemId)
    {
        profileObject[nameof(AIProfile.ItemId)] ??= fallbackItemId;

        if (profileObject[nameof(AIProfile.Properties)] is not JsonObject propertiesObject)
        {
            return;
        }

        RenameLegacyProperty(propertiesObject, _legacyFunctionInvocationMetadataKey, _functionInvocationMetadataKey);
        RenameLegacyProperty(propertiesObject, _legacyDataSourceMetadataKey, _dataSourceMetadataKey);
    }

    private static bool NormalizePersistedProfileDocument(JsonObject profileDocument)
    {
        var updated = false;

        if (profileDocument[nameof(AIProfile.Properties)] is JsonObject nestedProperties)
        {
            foreach (var property in nestedProperties)
            {
                if (profileDocument[property.Key] is null)
                {
                    profileDocument[property.Key] = property.Value?.DeepClone();
                    updated = true;
                }
            }

            profileDocument.Remove(nameof(AIProfile.Properties));
            updated = true;
        }

        updated |= RenameLegacyProperty(profileDocument, _legacyFunctionInvocationMetadataKey, _functionInvocationMetadataKey);
        updated |= RenameLegacyProperty(profileDocument, _legacyDataSourceMetadataKey, _dataSourceMetadataKey);
        updated |= RenameLegacyProperty(profileDocument, _legacyDocumentsMetadataKey, _documentsMetadataKey);
        updated |= RenameLegacyProperty(profileDocument, _legacyAnalyticsMetadataKey, _analyticsMetadataKey);
        updated |= RenameLegacyProperty(profileDocument, _legacyAzureRagMetadataKey, _dataSourceRagMetadataKey);

        return updated;
    }

    private static bool RenameLegacyProperty(JsonObject propertiesObject, string legacyKey, string newKey)
    {
        var updated = false;

        if (propertiesObject[newKey] is null &&
            propertiesObject[legacyKey] is JsonNode legacyNode)
        {
            propertiesObject[newKey] = legacyNode.DeepClone();
            updated = true;
        }

        if (propertiesObject.Remove(legacyKey))
        {
            updated = true;
        }

        return updated;
    }
}
