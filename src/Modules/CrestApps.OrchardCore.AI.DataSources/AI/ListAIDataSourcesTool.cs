using System.Text.Json;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Infrastructure;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Indexing;
using QueryContext = CrestApps.Core.Models.QueryContext;

namespace CrestApps.OrchardCore.AI.DataSources.AI;

/// <summary>
/// Lists the AI data sources, as the Data Sources page does, or returns one by ID, with the source types and the AI
/// knowledge base indexes a new data source can use. Credentials are reported only as stored or not.
/// </summary>
public sealed class ListAIDataSourcesTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listAIDataSources";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The ID of one data source to return. Leave it out to list data sources."
        },
        "search": {
          "type": "string",
          "description": "Text the data source's name contains."
        },
        "source": {
          "type": "string",
          "description": "Only data sources of this source type, such as SearchIndexProfile, Web or File."
        },
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page to return, from 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "How many data sources a page holds, at most 50. Defaults to 20."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the AI data sources (knowledge bases) with their source type, destination index and field mapping, with optional name search, source type filter, paging, or one data source by id. Also returns the source types that can be created on this site and the AI knowledge base indexes. Secrets are never returned, only whether one is stored.";

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

        if (!await AIModuleTools.IsAuthorizedAsync(services, AIPermissions.ManageAIDataSources))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var manager = services.GetRequiredService<ISourceCatalogManager<AIDataSource>>();
        var id = AIModuleTools.ReadString(arguments, "id");

        if (id is not null)
        {
            var dataSource = await manager.FindByIdAsync(id, cancellationToken);

            if (dataSource is null)
            {
                return $"No data source has the ID '{id}'. Call {TheName} without an id to see the data sources.";
            }

            return AIModuleTools.Json(await AIDataSourceToolDescriptions.DescribeAsync(services, dataSource));
        }

        var (page, pageSize) = AIModuleTools.ReadPaging(arguments);
        var result = await manager.PageAsync(page, pageSize, new QueryContext
        {
            Sorted = true,
            Name = AIModuleTools.ReadString(arguments, "search"),
            Source = AIModuleTools.ReadString(arguments, "source"),
        }, cancellationToken);
        var items = new List<object>();

        foreach (var dataSource in result.Entries)
        {
            items.Add(await AIDataSourceToolDescriptions.DescribeAsync(services, dataSource));
        }

        var fieldMapping = services.GetService<IOptions<AIDataSourceFieldMappingOptions>>()?.Value;
        var sources = services.GetService<IOptions<AIDataSourceSourceOptions>>()?.Value?.Sources ?? [];
        var indexes = await services.GetRequiredService<IIndexProfileStore>().GetByTypeAsync(DataSourceConstants.IndexingTaskType);

        return AIModuleTools.Json(new
        {
            items,
            total = result.Count,
            page,
            pageSize,
            truncated = page * pageSize < result.Count,
            sourceTypes = sources
                .OrderBy(source => source.DisplayName.Value, StringComparer.OrdinalIgnoreCase)
                .Select(source => new
                {
                    sourceType = source.SourceType,
                    displayName = source.DisplayName.Value,
                    description = source.Description.Value,
                    requiresFieldMapping = fieldMapping?.IsRequired(source.SourceType) ?? true,
                }),
            knowledgeBaseIndexes = indexes
                .OrderBy(index => index.Name, StringComparer.OrdinalIgnoreCase)
                .Select(index => new
                {
                    name = index.Name,
                    provider = index.ProviderName,
                }),
        });
    }
}
