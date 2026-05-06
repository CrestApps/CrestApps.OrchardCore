using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.AI;
using CrestApps.OrchardCore.AI.Core;
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

/// <summary>
/// Defines database migrations for the Migrations module.
/// </summary>
public sealed class AIDeploymentIndexMigrations : DataMigration
{
    private const string _legacyDeploymentDocumentTypePrefix =
        "CrestApps.OrchardCore.Models.DictionaryDocument`1[[CrestApps.OrchardCore.AI.Models.AIDeployment, CrestApps.OrchardCore.AI.Abstractions";
    private const string _legacyProfileDocumentTypePrefix =
        "CrestApps.OrchardCore.Models.DictionaryDocument`1[[CrestApps.OrchardCore.AI.Models.AIProfile, CrestApps.OrchardCore.AI.Abstractions";
    private const string _legacyConnectionDocumentTypePrefix =
        "CrestApps.OrchardCore.Models.DictionaryDocument`1[[CrestApps.OrchardCore.AI.Models.AIProviderConnection, CrestApps.OrchardCore.AI.Abstractions";
    private const string _legacyDeploymentIdPropertyName = "DeploymentId";
    private const string _legacyDeploymentNamePropertyName = "DeploymentName";
    private const string _legacyChatDeploymentIdPropertyName = "ChatDeploymentId";
    private const string _legacyChatDeploymentNamePropertyName = "ChatDeploymentName";
    private const string _legacyUtilityDeploymentIdPropertyName = "UtilityDeploymentId";
    private const string _legacyUtilityDeploymentNamePropertyName = "UtilityDeploymentName";
    private const string _legacyProfilePropertiesPropertyName = "Properties";

    private readonly YesSqlStoreOptions _option;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIDeploymentIndexMigrations"/> class.
    /// </summary>
    /// <param name="option">The option.</param>
    public AIDeploymentIndexMigrations(IOptions<YesSqlStoreOptions> option)
    {
        _option = option.Value;
    }

    /// <summary>
    /// Creates a new async.
    /// </summary>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateAIDeploymentIndexSchemaAsync(_option);

        ShellScope.AddDeferredTask(scope => ImportLegacyDeploymentsAsync(scope.ServiceProvider));

        return 3;
    }

    /// <summary>
    /// Updates the from1.
    /// </summary>
    public static int UpdateFrom1()
    {
        ShellScope.AddDeferredTask(scope => ImportLegacyDeploymentsAsync(scope.ServiceProvider));

        return 3;
    }

    /// <summary>
    /// Updates the from2.
    /// </summary>
    public static int UpdateFrom2()
    {
        ShellScope.AddDeferredTask(scope => ImportLegacyDeploymentsAsync(scope.ServiceProvider));

        return 3;
    }

    private static async Task ImportLegacyDeploymentsAsync(IServiceProvider serviceProvider)
    {
        var store = serviceProvider.GetRequiredService<IStore>();
        var dbConnectionAccessor = serviceProvider.GetRequiredService<IDbConnectionAccessor>();
        var deploymentManager = serviceProvider.GetRequiredService<IAIDeploymentManager>();
        var logger = serviceProvider.GetRequiredService<ILogger<AIDeploymentIndexMigrations>>();

        var dialect = store.Configuration.SqlDialect;
        var documentTableName = store.Configuration.TableNameConvention.GetDocumentTable(string.Empty);
        var table = $"{store.Configuration.TablePrefix}{documentTableName}";
        var quotedTableName = dialect.QuoteForTableName(table, store.Configuration.Schema);
        var quotedIdColumnName = dialect.QuoteForColumnName(nameof(Document.Id));
        var quotedTypeColumnName = dialect.QuoteForColumnName(nameof(Document.Type));
        var quotedContentColumnName = dialect.QuoteForColumnName(nameof(Document.Content));

        await using var connection = dbConnectionAccessor.CreateConnection();
        await connection.OpenAsync();

        var documents = await GetLegacyDocumentsAsync(
            connection,
            store,
            quotedIdColumnName,
            quotedTypeColumnName,
            quotedContentColumnName,
            quotedTableName,
            _legacyDeploymentDocumentTypePrefix);

        if (documents.Count == 0)
        {
            return;
        }

        var legacyProfileDeploymentTypeHints = await GetLegacyProfileDeploymentTypeHintsAsync(
            connection,
            store,
            quotedIdColumnName,
            quotedTypeColumnName,
            quotedContentColumnName,
            quotedTableName);
        var legacyConnections = await GetLegacyConnectionsAsync(
            connection,
            store,
            quotedIdColumnName,
            quotedTypeColumnName,
            quotedContentColumnName,
            quotedTableName);
        var importedCount = 0;
        var existingDeployments = (await deploymentManager.GetAllAsync()).ToList();

        foreach (var document in documents)
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

                deploymentObject[nameof(AIDeployment.ItemId)] ??= record.Key;

                var itemId = deploymentObject[nameof(AIDeployment.ItemId)]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(itemId))
                {
                    continue;
                }

                var connectionName = deploymentObject[nameof(AIDeployment.ConnectionName)]?.GetValue<string>()?.Trim();

#pragma warning disable CS0618 // Type or member is obsolete
                var sourceName = deploymentObject[nameof(AIDeployment.ClientName)]?.GetValue<string>()
                    ?? deploymentObject[nameof(AIDeployment.ProviderName)]?.GetValue<string>()
                    ?? deploymentObject[nameof(AIDeployment.Source)]?.GetValue<string>();
#pragma warning restore CS0618 // Type or member is obsolete
                var name = deploymentObject[nameof(AIDeployment.Name)]?.GetValue<string>()?.Trim();
                var modelName = deploymentObject[nameof(AIDeployment.ModelName)]?.GetValue<string>()?.Trim();

                var deploymentType = TryGetDeploymentType(deploymentObject[nameof(AIDeployment.Type)], out var parsedDeploymentType)
                    ? parsedDeploymentType
                    : InferLegacyDeploymentType(
                        itemId,
                        name,
                        connectionName,
                        sourceName,
                        legacyProfileDeploymentTypeHints.DeploymentTypesById,
                        legacyProfileDeploymentTypeHints.DeploymentTypesByName,
                        legacyConnections);
                var deployment = LegacyAIDeploymentMigrationHelper.FindWritableDeployment(
                    existingDeployments,
                    itemId,
                    name,
                    modelName,
                    sourceName,
                    connectionName);

                if (deployment is not null)
                {
                    await deploymentManager.UpdateAsync(deployment, deploymentObject);
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(sourceName))
                    {
                        logger.LogWarning(
                            "Skipping legacy AI deployment {ItemId} because no source/client name was found.",
                            itemId);
                        continue;
                    }

                    var uniqueName = LegacyAIDeploymentMigrationHelper.GenerateUniqueDeploymentName(existingDeployments, name);

                    if (!string.Equals(uniqueName, name, StringComparison.OrdinalIgnoreCase))
                    {
                        deploymentObject[nameof(AIDeployment.ModelName)] ??= name;
                        deploymentObject[nameof(AIDeployment.Name)] = uniqueName;

                        if (logger.IsEnabled(LogLevel.Information))
                        {
                            logger.LogInformation(
                                "Creating migrated AI deployment {ItemId} as '{UniqueName}' because '{OriginalName}' is already used by another deployment.",
                                itemId,
                                uniqueName,
                                name);
                        }
                    }

                    deployment = await deploymentManager.NewAsync(sourceName, deploymentObject);
                    deployment.ItemId = itemId;
                }

                if (deploymentType.IsValidSelection())
                {
                    deployment.Type = deployment.Type.IsValidSelection()
                        ? deployment.Type | deploymentType
                        : deploymentType;
                }

                var validationResult = await deploymentManager.ValidateAsync(deployment);
                if (!validationResult.Succeeded)
                {
                    logger.LogWarning(
                        "Skipping legacy AI deployment {ItemId} because validation failed: {Errors}",
                        itemId,
                        string.Join("; ", validationResult.Errors.Select(error => error.ErrorMessage)));
                    continue;
                }

                await deploymentManager.CreateAsync(deployment);
                UpsertExistingDeployment(existingDeployments, deployment);
                importedCount++;
            }
        }

        if (importedCount > 0 && logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Imported or updated {Count} legacy AI deployments from dictionary documents.",
                importedCount);
        }
    }

    private static void UpsertExistingDeployment(List<AIDeployment> deployments, AIDeployment deployment)
    {
        var existingDeployment = deployments.FirstOrDefault(item =>
            string.Equals(item.ItemId, deployment.ItemId, StringComparison.OrdinalIgnoreCase));

        if (existingDeployment is not null)
        {
            deployments.Remove(existingDeployment);
        }

        deployments.Add(deployment);
    }

    private static async Task<List<Document>> GetLegacyDocumentsAsync(
        System.Data.Common.DbConnection connection,
        IStore store,
        string quotedIdColumnName,
        string quotedTypeColumnName,
        string quotedContentColumnName,
        string quotedTableName,
        string documentTypePrefix)
    {
        var sqlBuilder = new SqlBuilder(store.Configuration.TablePrefix, store.Configuration.SqlDialect);
        sqlBuilder.AddSelector(quotedIdColumnName);
        sqlBuilder.AddSelector("," + quotedContentColumnName);
        sqlBuilder.From(quotedTableName);
        sqlBuilder.WhereAnd($" {quotedTypeColumnName} LIKE '{documentTypePrefix}%' ");

        return (await connection.QueryAsync<Document>(sqlBuilder.ToSqlString())).ToList();
    }

    private static async Task<LegacyProfileDeploymentTypeHints> GetLegacyProfileDeploymentTypeHintsAsync(
        System.Data.Common.DbConnection connection,
        IStore store,
        string quotedIdColumnName,
        string quotedTypeColumnName,
        string quotedContentColumnName,
        string quotedTableName)
    {
        var documents = await GetLegacyDocumentsAsync(
            connection,
            store,
            quotedIdColumnName,
            quotedTypeColumnName,
            quotedContentColumnName,
            quotedTableName,
            _legacyProfileDocumentTypePrefix);
        var deploymentTypesById = new Dictionary<string, AIDeploymentType>(StringComparer.OrdinalIgnoreCase);
        var deploymentTypesByName = new Dictionary<string, AIDeploymentType>(StringComparer.OrdinalIgnoreCase);

        foreach (var document in documents)
        {
            if (JsonNode.Parse(document.Content)?["Records"] is not JsonObject recordsObject)
            {
                continue;
            }

            foreach (var record in recordsObject)
            {
                if (record.Value is not JsonObject profileObject)
                {
                    continue;
                }

                AddLegacyProfileDeploymentTypeHint(profileObject, deploymentTypesById, deploymentTypesByName);
            }
        }

        return new LegacyProfileDeploymentTypeHints(deploymentTypesById, deploymentTypesByName);
    }

    private static async Task<List<AIProviderConnection>> GetLegacyConnectionsAsync(
        System.Data.Common.DbConnection connection,
        IStore store,
        string quotedIdColumnName,
        string quotedTypeColumnName,
        string quotedContentColumnName,
        string quotedTableName)
    {
        var documents = await GetLegacyDocumentsAsync(
            connection,
            store,
            quotedIdColumnName,
            quotedTypeColumnName,
            quotedContentColumnName,
            quotedTableName,
            _legacyConnectionDocumentTypePrefix);
        var legacyConnections = new List<AIProviderConnection>();

        foreach (var document in documents)
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

                connectionObject[nameof(AIProviderConnection.ItemId)] ??= record.Key;

#pragma warning disable CS0618 // Type or member is obsolete
                connectionObject[nameof(AIProviderConnection.ClientName)] ??= connectionObject[nameof(AIProviderConnection.Source)]?.DeepClone();
#pragma warning restore CS0618 // Type or member is obsolete

                if (JsonSerializer.Deserialize<AIProviderConnection>(connectionObject.ToJsonString()) is { } connectionModel)
                {
                    legacyConnections.Add(connectionModel);
                }
            }
        }

        return legacyConnections;
    }

    private static void AddLegacyProfileDeploymentTypeHint(
        JsonObject profileObject,
        IDictionary<string, AIDeploymentType> deploymentTypesById,
        IDictionary<string, AIDeploymentType> deploymentTypesByName)
    {
        AddDeploymentTypeHint(profileObject[_legacyDeploymentIdPropertyName], AIDeploymentType.Chat, deploymentTypesById);
        AddDeploymentTypeHint(profileObject[_legacyChatDeploymentIdPropertyName], AIDeploymentType.Chat, deploymentTypesById);
        AddDeploymentTypeHint(profileObject[_legacyUtilityDeploymentIdPropertyName], AIDeploymentType.Utility, deploymentTypesById);
        AddDeploymentTypeHint(profileObject[_legacyDeploymentNamePropertyName], AIDeploymentType.Chat, deploymentTypesByName);
        AddDeploymentTypeHint(profileObject[_legacyChatDeploymentNamePropertyName], AIDeploymentType.Chat, deploymentTypesByName);
        AddDeploymentTypeHint(profileObject[_legacyUtilityDeploymentNamePropertyName], AIDeploymentType.Utility, deploymentTypesByName);

        if (profileObject[_legacyProfilePropertiesPropertyName] is not JsonObject propertiesObject)
        {
            return;
        }

        AddDeploymentTypeHint(propertiesObject[_legacyDeploymentIdPropertyName], AIDeploymentType.Chat, deploymentTypesById);
        AddDeploymentTypeHint(propertiesObject[_legacyChatDeploymentIdPropertyName], AIDeploymentType.Chat, deploymentTypesById);
        AddDeploymentTypeHint(propertiesObject[_legacyUtilityDeploymentIdPropertyName], AIDeploymentType.Utility, deploymentTypesById);
        AddDeploymentTypeHint(propertiesObject[_legacyDeploymentNamePropertyName], AIDeploymentType.Chat, deploymentTypesByName);
        AddDeploymentTypeHint(propertiesObject[_legacyChatDeploymentNamePropertyName], AIDeploymentType.Chat, deploymentTypesByName);
        AddDeploymentTypeHint(propertiesObject[_legacyUtilityDeploymentNamePropertyName], AIDeploymentType.Utility, deploymentTypesByName);
    }

    private static void AddDeploymentTypeHint(
        JsonNode selectorNode,
        AIDeploymentType deploymentType,
        IDictionary<string, AIDeploymentType> hints)
    {
        if (selectorNode?.GetValue<string>()?.Trim() is not { Length: > 0 } selector)
        {
            return;
        }

        hints.TryGetValue(selector, out var existingType);
        hints[selector] = existingType | deploymentType;
    }

    private static AIDeploymentType InferLegacyDeploymentType(
        string itemId,
        string deploymentName,
        string connectionSelector,
        string sourceName,
        IReadOnlyDictionary<string, AIDeploymentType> profileDeploymentTypesById,
        IReadOnlyDictionary<string, AIDeploymentType> profileDeploymentTypesByName,
        IEnumerable<AIProviderConnection> legacyConnections)
    {
        var deploymentType = AIDeploymentType.None;

        if (!string.IsNullOrWhiteSpace(itemId) &&
            profileDeploymentTypesById.TryGetValue(itemId, out var profileTypeById))
        {
            deploymentType |= profileTypeById;
        }

        if (!string.IsNullOrWhiteSpace(deploymentName) &&
            profileDeploymentTypesByName.TryGetValue(deploymentName, out var profileTypeByName))
        {
            deploymentType |= profileTypeByName;
        }

        if (deploymentType.IsValidSelection())
        {
            return deploymentType;
        }

        deploymentType = InferDeploymentType(
            deploymentName,
            FindMatchingConnection(connectionSelector, sourceName, legacyConnections));

        return deploymentType.IsValidSelection()
            ? deploymentType
            : AIDeploymentType.Chat;
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
            return AIDeploymentType.None;
        }

        var deploymentType = AIDeploymentType.None;

        AddTypeIfMatch(ref deploymentType, deploymentName, connection.GetLegacyChatDeploymentName(), AIDeploymentType.Chat);
        AddTypeIfMatch(ref deploymentType, deploymentName, connection.GetLegacyUtilityDeploymentName(), AIDeploymentType.Utility);
        AddTypeIfMatch(ref deploymentType, deploymentName, connection.GetLegacyEmbeddingDeploymentName(), AIDeploymentType.Embedding);
        AddTypeIfMatch(ref deploymentType, deploymentName, connection.GetLegacyImageDeploymentName(), AIDeploymentType.Image);
        AddTypeIfMatch(ref deploymentType, deploymentName, connection.GetLegacySpeechToTextDeploymentName(), AIDeploymentType.SpeechToText);

        return deploymentType;
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

    private sealed record LegacyProfileDeploymentTypeHints(
        IReadOnlyDictionary<string, AIDeploymentType> DeploymentTypesById,
        IReadOnlyDictionary<string, AIDeploymentType> DeploymentTypesByName);
}
