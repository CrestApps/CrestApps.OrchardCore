using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Settings;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.AI.Migrations;

/// <summary>
/// Imports AI deployments directly from v1 dictionary documents and backfills
/// default deployment site settings without changing the preview-era migration path.
/// </summary>
internal sealed class AIDeploymentV1DocumentMigrations : DataMigration
{
    private const int _batchSize = 50;
    private const string _legacyDictionaryDocumentTypePrefix =
        "CrestApps.OrchardCore.Models.DictionaryDocument`1[[CrestApps.OrchardCore.AI.Models.AIDeployment, CrestApps.OrchardCore.AI.Abstractions, Version=1.";

    public static int Create()
    {
        ShellScope.AddDeferredTask(scope => ImportAndBackfillAsync(scope.ServiceProvider));

        return 1;
    }

    private static async Task ImportAndBackfillAsync(IServiceProvider serviceProvider)
    {
        await ImportLegacyDeploymentsAsync(serviceProvider);
        await TryBackfillDefaultDeploymentSettingsAsync(serviceProvider);
    }

    private static async Task ImportLegacyDeploymentsAsync(IServiceProvider serviceProvider)
    {
        var store = serviceProvider.GetRequiredService<IStore>();
        var dbConnectionAccessor = serviceProvider.GetRequiredService<IDbConnectionAccessor>();
        var deploymentManager = serviceProvider.GetRequiredService<IAIDeploymentManager>();
        var connectionManager = serviceProvider.GetRequiredService<INamedSourceCatalogManager<AIProviderConnection>>();
        var logger = serviceProvider.GetRequiredService<ILogger<AIDeploymentV1DocumentMigrations>>();

        var connections = (await connectionManager.GetAllAsync()).ToList();

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
                    if (record.Value is not JsonObject deploymentObject)
                    {
                        continue;
                    }

                    try
                    {
                        NormalizeLegacyDeploymentObject(deploymentObject, record.Key, connections);

                        var itemId = deploymentObject[nameof(AIDeployment.ItemId)]?.GetValue<string>();
                        var name = deploymentObject[nameof(AIDeployment.Name)]?.GetValue<string>()?.Trim();
#pragma warning disable CS0618 // Type or member is obsolete
                        var sourceName = deploymentObject[nameof(AIDeployment.ClientName)]?.GetValue<string>()?.Trim()
                            ?? deploymentObject[nameof(AIDeployment.ProviderName)]?.GetValue<string>()?.Trim()
                            ?? deploymentObject[nameof(AIDeployment.Source)]?.GetValue<string>()?.Trim();
#pragma warning restore CS0618 // Type or member is obsolete

                        if (string.IsNullOrWhiteSpace(itemId) &&
                            (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(sourceName)))
                        {
                            logger.LogWarning(
                                "Skipping v1 AI deployment '{Key}' because no usable item ID or name/source pair was found.",
                                record.Key);
                            continue;
                        }

                        AIDeployment deployment = null;

                        if (!string.IsNullOrWhiteSpace(itemId))
                        {
                            deployment = await deploymentManager.FindByIdAsync(itemId);
                        }

                        if (deployment is null &&
                            !string.IsNullOrWhiteSpace(name) &&
                            !string.IsNullOrWhiteSpace(sourceName))
                        {
                            deployment = await deploymentManager.GetAsync(name, sourceName);
                        }

                        if (deployment is not null)
                        {
                            await deploymentManager.UpdateAsync(deployment, deploymentObject);
                        }
                        else
                        {
                            if (string.IsNullOrWhiteSpace(sourceName))
                            {
                                logger.LogWarning(
                                    "Skipping v1 AI deployment {ItemId} because no source/client name was found.",
                                    itemId ?? record.Key);
                                continue;
                            }

                            deployment = await deploymentManager.NewAsync(sourceName, deploymentObject);

                            if (!string.IsNullOrWhiteSpace(itemId) && UniqueId.IsValid(itemId))
                            {
                                deployment.ItemId = itemId;
                            }
                        }

                        var validationResult = await deploymentManager.ValidateAsync(deployment);

                        if (!validationResult.Succeeded)
                        {
                            logger.LogWarning(
                                "Skipping v1 AI deployment {ItemId} because validation failed: {Errors}",
                                itemId ?? name ?? record.Key,
                                string.Join("; ", validationResult.Errors.Select(error => error.ErrorMessage)));
                            continue;
                        }

                        await deploymentManager.CreateAsync(deployment);
                        importedCount++;
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to migrate v1 AIDeployment with key '{Key}'. Skipping.", record.Key);
                    }
                }
            }
        }

        if (importedCount > 0 && logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Completed v1 AIDeployment migration. Migrated or updated {Count} deployments.", importedCount);
        }
    }

    private static async Task TryBackfillDefaultDeploymentSettingsAsync(IServiceProvider serviceProvider)
    {
        var siteService = serviceProvider.GetRequiredService<ISiteService>();
        var deploymentManager = serviceProvider.GetRequiredService<IAIDeploymentManager>();
        var connectionManager = serviceProvider.GetRequiredService<INamedSourceCatalogManager<AIProviderConnection>>();

        var settings = await siteService.LoadSiteSettingsAsync();
        var deployments = (await deploymentManager.GetAllAsync()).ToList();

        if (deployments.Count == 0)
        {
            return;
        }

        var connections = (await connectionManager.GetAllAsync()).ToList();
        var updated = false;

        settings.Alter<DefaultAIDeploymentSettings>(siteSettings =>
        {
            updated = TryPopulateDefaultDeploymentSettings(siteSettings, connections, deployments);
        });

        if (!updated)
        {
            return;
        }

        await siteService.UpdateSiteSettingsAsync(settings);
    }

    private static IEnumerable<IReadOnlyList<Document>> BatchDocuments(List<Document> documents)
    {
        for (var index = 0; index < documents.Count; index += _batchSize)
        {
            yield return documents.Skip(index).Take(_batchSize).ToList();
        }
    }

    private static void NormalizeLegacyDeploymentObject(
        JsonObject deploymentObject,
        string fallbackItemId,
        IEnumerable<AIProviderConnection> connections)
    {
        deploymentObject[nameof(AIDeployment.ItemId)] ??= deploymentObject["Id"]?.DeepClone() ?? fallbackItemId;

        if (deploymentObject[nameof(AIDeployment.ModelName)] is null &&
            deploymentObject[nameof(AIDeployment.Name)] is JsonNode deploymentNameNode)
        {
            deploymentObject[nameof(AIDeployment.ModelName)] = deploymentNameNode.DeepClone();
        }

        var connection = FindMatchingConnection(
            deploymentObject[nameof(AIDeployment.ConnectionName)]?.GetValue<string>(),
#pragma warning disable CS0618 // Type or member is obsolete
            deploymentObject[nameof(AIDeployment.Source)]?.GetValue<string>()
#pragma warning restore CS0618 // Type or member is obsolete
            ?? deploymentObject[nameof(AIDeployment.ClientName)]?.GetValue<string>(),
            connections);

        if (connection is not null)
        {
            deploymentObject[nameof(AIDeployment.ClientName)] ??= connection.ClientName;

            if (deploymentObject[nameof(AIDeployment.ConnectionName)] is null)
            {
                deploymentObject[nameof(AIDeployment.ConnectionName)] = connection.ItemId;
            }
        }
        else
        {
#pragma warning disable CS0618 // Type or member is obsolete
            deploymentObject[nameof(AIDeployment.ClientName)] ??= deploymentObject[nameof(AIDeployment.Source)]?.DeepClone();
#pragma warning restore CS0618 // Type or member is obsolete
        }

        if (!TryGetDeploymentType(deploymentObject[nameof(AIDeployment.Type)], out _))
        {
            deploymentObject[nameof(AIDeployment.Type)] = CreateTypeNode(
                InferDeploymentType(
                    deploymentObject[nameof(AIDeployment.Name)]?.GetValue<string>(),
                    connection));
        }
    }

    private static JsonObject EnsurePropertiesObject(JsonObject deploymentObject)
    {
        if (deploymentObject[nameof(AIDeployment.Properties)] is JsonObject propertiesObject)
        {
            return propertiesObject;
        }

        propertiesObject = [];
        deploymentObject[nameof(AIDeployment.Properties)] = propertiesObject;

        return propertiesObject;
    }

    private static AIProviderConnection FindMatchingConnection(
        string connectionSelector,
        string sourceName,
        IEnumerable<AIProviderConnection> connections)
    {
        if (string.IsNullOrWhiteSpace(connectionSelector))
        {
            return null;
        }

        return connections.FirstOrDefault(connection =>
            (string.IsNullOrWhiteSpace(sourceName) ||
                string.Equals(connection.ClientName, sourceName, StringComparison.OrdinalIgnoreCase)) &&
            (string.Equals(connection.ItemId, connectionSelector, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(connection.Name, connectionSelector, StringComparison.OrdinalIgnoreCase)));
    }

    private static AIDeploymentType InferDeploymentType(string deploymentName, AIProviderConnection connection)
    {
        if (string.IsNullOrWhiteSpace(deploymentName) || connection is null)
        {
            return AIDeploymentType.Chat;
        }

        var deploymentType = AIDeploymentType.None;

        AddTypeIfMatch(ref deploymentType, deploymentName, connection.GetLegacyChatDeploymentName(), AIDeploymentType.Chat);
        AddTypeIfMatch(ref deploymentType, deploymentName, connection.GetLegacyUtilityDeploymentName(), AIDeploymentType.Utility);
        AddTypeIfMatch(ref deploymentType, deploymentName, connection.GetLegacyEmbeddingDeploymentName(), AIDeploymentType.Embedding);
        AddTypeIfMatch(ref deploymentType, deploymentName, connection.GetLegacyImageDeploymentName(), AIDeploymentType.Image);
        AddTypeIfMatch(ref deploymentType, deploymentName, connection.GetLegacySpeechToTextDeploymentName(), AIDeploymentType.SpeechToText);

        return deploymentType.IsValidSelection()
            ? deploymentType
            : AIDeploymentType.Chat;
    }

    private static void AddTypeIfMatch(
        ref AIDeploymentType deploymentType,
        string deploymentName,
        string expectedName,
        AIDeploymentType type)
    {
        if (!string.IsNullOrWhiteSpace(expectedName) &&
            string.Equals(deploymentName, expectedName, StringComparison.OrdinalIgnoreCase))
        {
            deploymentType |= type;
        }
    }

    private static JsonNode CreateTypeNode(AIDeploymentType deploymentType)
    {
        var supportedTypes = Enum.GetValues<AIDeploymentType>()
            .Where(type =>
                type != AIDeploymentType.None &&
                deploymentType.HasFlag(type))
            .Select(type => (JsonNode)type.ToString())
            .ToList();

        return supportedTypes.Count == 1
            ? supportedTypes[0]
            : new JsonArray(supportedTypes.ToArray());
    }

    private static bool TryPopulateDefaultDeploymentSettings(
        DefaultAIDeploymentSettings settings,
        IEnumerable<AIProviderConnection> connections,
        IEnumerable<AIDeployment> deployments)
    {
        var updated = false;

        updated |= TryPopulateDefaultDeploymentName(
            settings.DefaultChatDeploymentName,
            value => settings.DefaultChatDeploymentName = value,
            FindPreferredDeploymentName(
                deployments,
                connections,
                AIDeploymentType.Chat,
                static connection => connection.GetLegacyChatDeploymentName()));

        updated |= TryPopulateDefaultDeploymentName(
            settings.DefaultUtilityDeploymentName,
            value => settings.DefaultUtilityDeploymentName = value,
            FindPreferredDeploymentName(
                deployments,
                connections,
                AIDeploymentType.Utility,
                static connection => connection.GetLegacyUtilityDeploymentName()));

        updated |= TryPopulateDefaultDeploymentName(
            settings.DefaultEmbeddingDeploymentName,
            value => settings.DefaultEmbeddingDeploymentName = value,
            FindPreferredDeploymentName(
                deployments,
                connections,
                AIDeploymentType.Embedding,
                static connection => connection.GetLegacyEmbeddingDeploymentName()));

        updated |= TryPopulateDefaultDeploymentName(
            settings.DefaultImageDeploymentName,
            value => settings.DefaultImageDeploymentName = value,
            FindPreferredDeploymentName(
                deployments,
                connections,
                AIDeploymentType.Image,
                static connection => connection.GetLegacyImageDeploymentName()));

        updated |= TryPopulateDefaultDeploymentName(
            settings.DefaultSpeechToTextDeploymentName,
            value => settings.DefaultSpeechToTextDeploymentName = value,
            FindPreferredDeploymentName(
                deployments,
                connections,
                AIDeploymentType.SpeechToText,
                static connection => connection.GetLegacySpeechToTextDeploymentName()));

        updated |= TryPopulateDefaultDeploymentName(
            settings.DefaultTextToSpeechDeploymentName,
            value => settings.DefaultTextToSpeechDeploymentName = value,
            FindPreferredDeploymentName(
                deployments,
                connections,
                AIDeploymentType.TextToSpeech));

        return updated;
    }

    private static bool TryPopulateDefaultDeploymentName(
        string currentValue,
        Action<string> assign,
        string newValue)
    {
        if (!string.IsNullOrWhiteSpace(currentValue) || string.IsNullOrWhiteSpace(newValue))
        {
            return false;
        }

        assign(newValue);

        return true;
    }

    private static string FindPreferredDeploymentName(
        IEnumerable<AIDeployment> deployments,
        IEnumerable<AIProviderConnection> connections,
        AIDeploymentType type,
        Func<AIProviderConnection, string> legacyDeploymentNameAccessor = null)
    {
        var candidates = deployments
            .Where(deployment => deployment.SupportsType(type))
            .OrderBy(deployment => deployment.ConnectionName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(deployment => deployment.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var uiDefault = candidates
            .FirstOrDefault(static deployment => deployment.GetIsDefault())?
            .Name;

        if (!string.IsNullOrWhiteSpace(uiDefault))
        {
            return uiDefault;
        }

        if (legacyDeploymentNameAccessor != null)
        {
            foreach (var connection in connections
                .Where(connection => !string.IsNullOrWhiteSpace(legacyDeploymentNameAccessor(connection)))
                .OrderBy(connection => connection.Name, StringComparer.OrdinalIgnoreCase))
            {
                var deploymentName = FindConnectionDeploymentName(type, connection, candidates, legacyDeploymentNameAccessor(connection));

                if (!string.IsNullOrWhiteSpace(deploymentName))
                {
                    return deploymentName;
                }
            }
        }

        return candidates.FirstOrDefault()?.Name;
    }

    private static string FindConnectionDeploymentName(
        AIDeploymentType type,
        AIProviderConnection connection,
        IEnumerable<AIDeployment> deployments,
        string deploymentName)
    {
        return deployments
            .Where(deployment =>
                deployment.SupportsType(type) &&
                string.Equals(deployment.Name, deploymentName, StringComparison.OrdinalIgnoreCase) &&
                (string.Equals(deployment.ConnectionName, connection.ItemId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(deployment.ConnectionName, connection.Name, StringComparison.OrdinalIgnoreCase)))
            .Select(deployment => deployment.Name)
            .FirstOrDefault()
            ?? deployments
                .Where(deployment =>
                    deployment.SupportsType(type) &&
                    string.Equals(deployment.Name, deploymentName, StringComparison.OrdinalIgnoreCase))
                .Select(deployment => deployment.Name)
                .FirstOrDefault();
    }

    private static bool TryGetDeploymentType(JsonNode typeNode, out AIDeploymentType type)
    {
        type = AIDeploymentType.None;

        if (typeNode is null)
        {
            return false;
        }

        if (typeNode is JsonArray array)
        {
            foreach (var item in array)
            {
                if (item is null ||
                    !Enum.TryParse<AIDeploymentType>(item.GetValue<string>(), ignoreCase: true, out var parsedType) ||
                    parsedType == AIDeploymentType.None)
                {
                    type = AIDeploymentType.None;

                    return false;
                }

                type |= parsedType;
            }

            return type.IsValidSelection();
        }

        var typeValue = typeNode.GetValue<string>();

        return !string.IsNullOrEmpty(typeValue) &&
            Enum.TryParse(typeValue, ignoreCase: true, out type) &&
            type.IsValidSelection();
    }
}
