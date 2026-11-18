using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Infrastructure;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Agent.Profiles;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Extensions;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.DataConnectors;
using CrestApps.OrchardCore.DataConnectors.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Indexing;

namespace CrestApps.OrchardCore.AI.Agent.DataSources;

/// <summary>
/// Creates an AI data source, or renames and remaps one by ID, with the checks of the data source editors, the data
/// source catalog and the source's own handler. No API key, password or connection string is taken: an Azure AI Search
/// or Elasticsearch data source is created on a data connection, a PostgreSQL one on the site's connection string, and
/// a person enters any other credentials on the editor. The data source of the assistant running the turn cannot be
/// changed.
/// </summary>
public sealed class SaveAIDataSourceTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveAIDataSource";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The ID of the data source to update, from listAIDataSources. Leave it out to create one."
        },
        "source": {
          "type": "string",
          "description": "When creating, the source type, from the sourceTypes listAIDataSources returns, such as SearchIndexProfile, Web, File, AzureAISearch, Elasticsearch or PostgreSQL. A data source's source type cannot change."
        },
        "displayText": { "type": "string", "description": "The data source's name. Required when creating." },
        "aiKnowledgeBaseIndexProfileName": { "type": "string", "description": "The AI knowledge base index the content is stored in, from the knowledgeBaseIndexes listAIDataSources returns. Required when creating." },
        "sourceIndexProfileName": { "type": "string", "description": "For a SearchIndexProfile data source, the search index it reads from. Required for that source type." },
        "keyFieldName": { "type": "string", "description": "The source field that holds each document's key. Leave it out to use the document's own key." },
        "titleFieldName": { "type": "string", "description": "The source field that holds each document's title." },
        "contentFieldName": { "type": "string", "description": "The source field that holds each document's text. Required for the source types whose requiresFieldMapping is true." },
        "dataConnectionId": { "type": "string", "description": "When creating an AzureAISearch or Elasticsearch data source, the ID of the data connection that holds its address and credentials. Required for those source types." },
        "indexName": { "type": "string", "description": "When creating an AzureAISearch or Elasticsearch data source, the index to read in that service. Required for those source types." },
        "tableName": { "type": "string", "description": "When creating a PostgreSQL data source, the table to read, such as public.articles. It uses the site's configured PostgreSQL connection string." }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    private static readonly string[] _mappingArguments = ["aiKnowledgeBaseIndexProfileName", "sourceIndexProfileName", "keyFieldName", "titleFieldName", "contentFieldName"];

    private static readonly string[] _providerArguments = ["dataConnectionId", "indexName", "tableName"];

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates an AI data source of a given source type, or updates an existing one by id. An existing data source whose configuration is locked can only be renamed. Azure AI Search and Elasticsearch data sources are created on a data connection and PostgreSQL ones on the site's connection string; no API key, password or connection string is ever taken. Saving queues a full synchronization that generates embeddings in the background. The data source the running assistant uses cannot be changed.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, AIPermissions.ManageAIDataSources))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var manager = services.GetRequiredService<ISourceCatalogManager<AIDataSource>>();
        var id = AIModuleTools.ReadString(arguments, "id");
        AIDataSource existing = null;

        if (id is not null)
        {
            existing = await manager.FindByIdAsync(id, cancellationToken);

            if (existing is null)
            {
                return $"Nothing was saved: no data source has the ID '{id}'. Call listAIDataSources to find it.";
            }
        }

        var secret = AIModuleTools.FindSecretArgument(arguments);

        if (secret is not null)
        {
            var page = existing is null
                ? AIDataSourceToolDescriptions.GetCreatePath(services, AIModuleTools.ReadString(arguments, "source"))
                : AIDataSourceToolDescriptions.GetEditPath(services, existing.ItemId);

            return $"Nothing was saved: '{secret}' looks like a key, a password or a connection string, and tools never take secrets. Use a data connection or the site's connection, or let a person enter it on the data source's page: {page}";
        }

        AIDataSource working;

        if (existing is not null)
        {
            var protectedConfiguration = await AIConfigurationGuard.GetProtectedAsync(services, cancellationToken);

            if (protectedConfiguration.IsProtectedDataSource(existing.ItemId))
            {
                return AIProfileToolSupport.RunningAssistantRefusal($"the data source '{existing.DisplayText}'", AIDataSourceToolDescriptions.GetEditPath(services, existing.ItemId));
            }

            var given = _providerArguments.Where(arguments.ContainsKey).ToArray();

            if (given.Length > 0)
            {
                return $"Nothing was saved: {string.Join(", ", given)} can only be given when a data source is created. Change the connection settings on the data source's page: {AIDataSourceToolDescriptions.GetEditPath(services, existing.ItemId)}";
            }

            if (existing.IsConfigurationLocked())
            {
                var mapping = _mappingArguments.Where(arguments.ContainsKey).ToArray();

                if (mapping.Length > 0)
                {
                    return $"Nothing was saved: the configuration of '{existing.DisplayText}' is locked once its knowledge base is built, so only its name can change, not {string.Join(", ", mapping)}. Create a new data source for another mapping.";
                }
            }

            working = existing.Clone();
        }
        else
        {
            var sourceType = AIModuleTools.ReadString(arguments, "source");
            var sources = services.GetService<IOptions<AIDataSourceSourceOptions>>()?.Value?.Sources ?? [];
            var descriptor = sources.FirstOrDefault(entry => string.Equals(entry.SourceType, sourceType, StringComparison.OrdinalIgnoreCase));

            if (descriptor is null)
            {
                return $"Nothing was saved: name the source type in source, one of {string.Join(", ", sources.Select(entry => entry.SourceType))}.";
            }

            // As the data source editor, only a person who may manage data connections picks one.
            if (arguments.ContainsKey("dataConnectionId") &&
                !await AgentToolAuthorization.IsAuthorizedAsync(services, DataConnectorPermissions.ManageDataConnections))
            {
                return "Nothing was saved: you may not manage data connections, so you cannot pick one for a data source either.";
            }

            working = await manager.NewAsync(descriptor.SourceType, cancellationToken: cancellationToken);
        }

        var errors = new List<string>();
        var changes = await ReadChangesAsync(services, arguments, working, isNew: existing is null, errors, cancellationToken);

        if (errors.Count > 0)
        {
            return "Nothing was saved: " + string.Join(" ", errors);
        }

        changes(working);

        var validation = await manager.ValidateAsync(working, cancellationToken);

        // The source's own checks, which its editor and its readers apply, such as a data connection that fits.
        if (services.GetKeyedService<IAIDataSourceSourceHandler>(working.GetSourceType()) is { } sourceHandler)
        {
            await sourceHandler.ValidateAsync(working, validation, cancellationToken);
        }

        if (!validation.Succeeded)
        {
            return "Nothing was saved: " + string.Join(" ", validation.Errors.Select(error => error.ErrorMessage).Distinct(StringComparer.Ordinal));
        }

        if (existing is null)
        {
            await manager.CreateAsync(working, cancellationToken);
            existing = working;
        }
        else
        {
            changes(existing);
            await manager.UpdateAsync(existing, cancellationToken: cancellationToken);
        }

        return AgentToolResults.Json(new
        {
            saved = id is null ? "created" : "updated",
            dataSource = await AIDataSourceToolDescriptions.DescribeAsync(services, existing),
            synchronization = "A full synchronization of the data source into its AI knowledge base index was queued. It runs in the background and generates embeddings, which costs embedding tokens; it has not finished yet.",
        });
    }

    private static async Task<Action<AIDataSource>> ReadChangesAsync(IServiceProvider services, AIFunctionArguments arguments, AIDataSource current, bool isNew, List<string> errors, CancellationToken cancellationToken)
    {
        var sourceType = current.GetSourceType();
        var displayText = AIModuleTools.ReadString(arguments, "displayText");
        var knowledgeBase = AIModuleTools.ReadString(arguments, "aiKnowledgeBaseIndexProfileName");
        var sourceIndex = AIModuleTools.ReadString(arguments, "sourceIndexProfileName");
        var keyField = AIModuleTools.ReadString(arguments, "keyFieldName");
        var titleField = AIModuleTools.ReadString(arguments, "titleFieldName");
        var contentField = AIModuleTools.ReadString(arguments, "contentFieldName");
        var indexStore = services.GetRequiredService<IIndexProfileStore>();
        var fieldMapping = services.GetService<IOptions<AIDataSourceFieldMappingOptions>>()?.Value;

        // The data source editor's checks.
        if (string.IsNullOrWhiteSpace(displayText ?? current.DisplayText))
        {
            errors.Add("The name is required: give it in displayText.");
        }

        if (string.IsNullOrWhiteSpace(knowledgeBase ?? current.AIKnowledgeBaseIndexProfileName))
        {
            errors.Add("The destination index is required: give it in aiKnowledgeBaseIndexProfileName.");
        }
        else if (knowledgeBase is not null)
        {
            // The editor offers only the AI knowledge base indexes.
            var index = await indexStore.FindByNameAsync(knowledgeBase);

            if (index is null || !string.Equals(index.Type, DataSourceConstants.IndexingTaskType, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"'{knowledgeBase}' is not an AI knowledge base index. Pick one of the knowledgeBaseIndexes listAIDataSources returns.");
            }
        }

        if ((fieldMapping?.IsRequired(sourceType) ?? true) && string.IsNullOrWhiteSpace(contentField ?? current.ContentFieldName))
        {
            errors.Add("The content field is required: give it in contentFieldName.");
        }

        if (string.Equals(sourceType, AIDataSourceSourceTypes.SearchIndexProfile, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(sourceIndex ?? current.SourceIndexProfileName))
            {
                errors.Add("The source index is required: give it in sourceIndexProfileName.");
            }
            else if (sourceIndex is not null && await indexStore.FindByNameAsync(sourceIndex) is null)
            {
                errors.Add($"No search index is named '{sourceIndex}'.");
            }
        }
        else if (sourceIndex is not null)
        {
            errors.Add($"sourceIndexProfileName applies only to a SearchIndexProfile data source, not to a {sourceType} one.");
        }

        var provider = isNew ? ReadProvider(services, arguments, sourceType, errors) : null;

        return dataSource =>
        {
            if (displayText is not null)
            {
                dataSource.DisplayText = displayText;
            }

            if (knowledgeBase is not null)
            {
                dataSource.AIKnowledgeBaseIndexProfileName = knowledgeBase;
            }

            if (sourceIndex is not null)
            {
                dataSource.SourceIndexProfileName = sourceIndex;
            }

            if (keyField is not null)
            {
                dataSource.KeyFieldName = keyField;
            }

            if (titleField is not null)
            {
                dataSource.TitleFieldName = titleField;
            }

            if (contentField is not null)
            {
                dataSource.ContentFieldName = contentField;
            }

            provider?.Invoke(dataSource);
        };
    }

    private static Action<AIDataSource> ReadProvider(IServiceProvider services, AIFunctionArguments arguments, string sourceType, List<string> errors)
    {
        var connectionId = AIModuleTools.ReadString(arguments, "dataConnectionId");
        var indexName = AIModuleTools.ReadString(arguments, "indexName");
        var tableName = AIModuleTools.ReadString(arguments, "tableName");
        var isAzure = string.Equals(sourceType, AIDataSourceSourceTypes.AzureAISearch, StringComparison.OrdinalIgnoreCase);
        var isElasticsearch = string.Equals(sourceType, AIDataSourceSourceTypes.Elasticsearch, StringComparison.OrdinalIgnoreCase);
        var isPostgreSql = string.Equals(sourceType, AIDataSourceSourceTypes.PostgreSQL, StringComparison.OrdinalIgnoreCase);
        var createPath = AIDataSourceToolDescriptions.GetCreatePath(services, sourceType);

        if (isAzure || isElasticsearch)
        {
            // The editor's own endpoint, key and Elasticsearch default connection settings are not offered: a data
            // connection keeps the address and the credentials out of the tool.
            if (connectionId is null)
            {
                errors.Add($"A {sourceType} data source is created here on a data connection: give its ID in dataConnectionId. To enter an address and a key instead, create it on its page: {createPath}");
            }
            else if (services.GetService<IDataConnectionResolver>() is null)
            {
                errors.Add($"Data connections are not available on this site, so a {sourceType} data source is created on its page: {createPath}");
            }

            if (indexName is null)
            {
                errors.Add($"The {sourceType} index name is required: give it in indexName.");
            }
        }
        else if (connectionId is not null || indexName is not null)
        {
            errors.Add($"dataConnectionId and indexName apply only to an AzureAISearch or Elasticsearch data source.");
        }

        if (isPostgreSql)
        {
            if (tableName is null)
            {
                errors.Add("The PostgreSQL table name is required: give it in tableName.");
            }
        }
        else if (tableName is not null)
        {
            errors.Add("tableName applies only to a PostgreSQL data source.");
        }

        return dataSource =>
        {
            if (isAzure)
            {
                dataSource.Put(new DataConnectionReference { ConnectionId = connectionId });
                dataSource.Put(new AzureAISearchSourceMetadata { IndexName = indexName });
            }
            else if (isElasticsearch)
            {
                dataSource.Put(new DataConnectionReference { ConnectionId = connectionId });
                dataSource.Put(new ElasticsearchSourceMetadata { IndexName = indexName });
            }
            else if (isPostgreSql)
            {
                // No connection string of its own: the reader uses the site's configured one.
                dataSource.Put(new PostgreSQLSourceMetadata { TableName = tableName });
            }
        };
    }
}
