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
    private const int _batchSize = 50;
    private const string _legacyDocumentTypePrefix =
        "CrestApps.OrchardCore.Models.DictionaryDocument`1[[CrestApps.OrchardCore.AI.Models.AIDeployment, CrestApps.OrchardCore.AI.Abstractions";

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

        return 2;
    }

    /// <summary>
    /// Updates the from1.
    /// </summary>
    public static int UpdateFrom1()
    {
        ShellScope.AddDeferredTask(scope => ImportLegacyDeploymentsAsync(scope.ServiceProvider));

        return 2;
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

#pragma warning disable CS0618 // Type or member is obsolete
                    var sourceName = deploymentObject[nameof(AIDeployment.ClientName)]?.GetValue<string>()
                        ?? deploymentObject[nameof(AIDeployment.ProviderName)]?.GetValue<string>()
                        ?? deploymentObject[nameof(AIDeployment.Source)]?.GetValue<string>();
#pragma warning restore CS0618 // Type or member is obsolete
                    var name = deploymentObject[nameof(AIDeployment.Name)]?.GetValue<string>()?.Trim();

                    AIDeployment deployment = await deploymentManager.FindByIdAsync(itemId);

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
                                "Skipping legacy AI deployment {ItemId} because no source/client name was found.",
                                itemId);
                            continue;
                        }

                        deployment = await deploymentManager.NewAsync(sourceName, deploymentObject);
                        deployment.ItemId = itemId;
                    }

                    if (TryGetDeploymentType(deploymentObject[nameof(AIDeployment.Type)], out var deploymentType))
                    {
                        deployment.Type = deploymentType;
                    }

                    deployment.SetIsDefault(deploymentObject["IsDefault"]?.GetValue<bool>() ?? false);

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
                    importedCount++;
                }
            }
        }

        if (importedCount > 0 && logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Imported or updated {Count} legacy AI deployments from dictionary documents.",
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
