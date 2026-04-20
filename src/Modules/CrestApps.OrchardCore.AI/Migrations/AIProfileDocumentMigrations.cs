using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
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
/// Migrates existing AIProfile data from the legacy single-document DictionaryDocument storage
/// into individual YesSql documents stored in the AI collection. This ensures each profile is
/// stored as a separate row, avoiding SQL Server's nvarchar(max) size limitations and improving
/// scalability.
/// </summary>
internal sealed class AIProfileDocumentMigrations : DataMigration
{
    private const int _batchSize = 50;
    private const string _legacyNestedPropertiesKey = nameof(AIProfile.Properties);
    private const string _legacyFunctionInvocationMetadataKey = "AIProfileFunctionInvocationMetadata";
    private const string _legacyDataSourceMetadataKey = "AIProfileDataSourceMetadata";
    private const string _functionInvocationMetadataKey = nameof(FunctionInvocationMetadata);
    private const string _dataSourceMetadataKey = "DataSourceMetadata";

    // Match the DictionaryDocument<AIProfile> type name pattern used by YesSql.
    private const string _dictionaryDocumentTypePrefix =
        "CrestApps.OrchardCore.Models.DictionaryDocument`1[[CrestApps.OrchardCore.AI.Models.AIProfile, CrestApps.OrchardCore.AI.Abstractions, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null]], CrestApps.OrchardCore.Abstractions";

    public static int Create()
    {
        ShellScope.AddDeferredTask(scope => ImportLegacyProfilesAsync(scope.ServiceProvider));

        return 2;
    }

    public static int UpdateFrom1()
    {
        ShellScope.AddDeferredTask(scope => ImportLegacyProfilesAsync(scope.ServiceProvider));

        return 2;
    }

    private static async Task ImportLegacyProfilesAsync(IServiceProvider serviceProvider)
    {
        var store = serviceProvider.GetRequiredService<IStore>();
        var dbConnectionAccessor = serviceProvider.GetRequiredService<IDbConnectionAccessor>();
        var profileManager = serviceProvider.GetRequiredService<IAIProfileManager>();
        var logger = serviceProvider.GetRequiredService<ILogger<AIProfileDocumentMigrations>>();

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
        sqlBuilder.WhereAnd($" {quotedTypeColumnName} = '{_dictionaryDocumentTypePrefix}' ");

        try
        {
            var documents = (await connection.QueryAsync<Document>(sqlBuilder.ToSqlString())).ToList();

            if (documents.Count == 0)
            {
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug("No DictionaryDocument<AIProfile> found. Skipping migration.");
                }

                return;
            }

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Found {Count} DictionaryDocument(s) containing AIProfile records to migrate.", documents.Count);
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

                            NormalizeMigratedProfile(profile);

                            var validationResult = await profileManager.ValidateAsync(profile);
                            if (!validationResult.Succeeded)
                            {
                                logger.LogWarning(
                                    "Skipping legacy AI profile {ItemId} because validation failed: {Errors}",
                                    itemId ?? name ?? record.Key,
                                    string.Join("; ", validationResult.Errors.Select(error => error.ErrorMessage)));
                                continue;
                            }

                            await profileManager.CreateAsync(profile);
                            totalMigrated++;
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Failed to migrate AIProfile with key '{Key}'. Skipping.", record.Key);
                        }
                    }
                }
            }

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Completed AIProfile migration. Migrated or updated {Count} profiles.", totalMigrated);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while migrating AIProfile documents from DictionaryDocument to individual documents.");

            throw;
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

    private static void NormalizeMigratedProfile(AIProfile profile)
    {
        if (profile.Properties.TryGetValue(_legacyNestedPropertiesKey, out var nestedPropertiesValue) &&
            TryConvertToJsonObject(nestedPropertiesValue) is { Count: > 0 } nestedProperties)
        {
            foreach (var property in nestedProperties)
            {
                profile.Properties.TryAdd(property.Key, property.Value?.DeepClone());
            }
        }

        profile.Properties.Remove(_legacyNestedPropertiesKey);
        RenameLegacyProperty(profile.Properties, _legacyFunctionInvocationMetadataKey, _functionInvocationMetadataKey);
        RenameLegacyProperty(profile.Properties, _legacyDataSourceMetadataKey, _dataSourceMetadataKey);
    }

    private static void RenameLegacyProperty(JsonObject propertiesObject, string legacyKey, string newKey)
    {
        if (propertiesObject[newKey] is null &&
            propertiesObject[legacyKey] is JsonNode legacyNode)
        {
            propertiesObject[newKey] = legacyNode.DeepClone();
        }

        propertiesObject.Remove(legacyKey);
    }

    private static void RenameLegacyProperty(IDictionary<string, object> properties, string legacyKey, string newKey)
    {
        if (!properties.TryGetValue(legacyKey, out var legacyValue))
        {
            return;
        }

        if (!properties.ContainsKey(newKey))
        {
            properties[newKey] = legacyValue;
        }

        properties.Remove(legacyKey);
    }

    private static JsonObject TryConvertToJsonObject(object value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is JsonObject jsonObject)
        {
            return jsonObject;
        }

        if (value is JsonNode jsonNode)
        {
            return jsonNode as JsonObject;
        }

        return JsonSerializer.SerializeToNode(value) as JsonObject;
    }
}
