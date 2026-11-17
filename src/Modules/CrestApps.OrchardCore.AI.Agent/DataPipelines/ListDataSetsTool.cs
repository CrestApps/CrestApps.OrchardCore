using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.AI.Agent.DataPipelines;

/// <summary>
/// Lists the data sets the caller may read from every registered data source, as the pipeline designer's Add a data
/// source dialog does, with an optional text and data source filter and paging.
/// </summary>
public sealed class ListDataSetsTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listDataSets";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "query": {
          "type": "string",
          "description": "Only list the data sets whose name, display name, description or group contains this text."
        },
        "source": {
          "type": "string",
          "description": "Only list the data sets of this data source, by its technical name (the part before '::', such as Contents, Users or Queries) or its display name."
        },
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page to return, starting at 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "The number of data sets per page, 20 by default and 50 at most."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the data sets the current user may read (content types, users, saved queries and module data), with their Source::DataSet value, name, description, data source and group; optional text and data source filter. Use it to pick what a \"Read a data source\" step reads.";

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

        if (!await AIToolCallerAuthorization.IsAuthorizedAsync(services, OrchardCorePermissions.ManageDataPipelines))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var query = arguments.GetFirstValueOrDefault<string>("query")?.Trim();
        var source = arguments.GetFirstValueOrDefault<string>("source")?.Trim();
        var (page, pageSize) = AgentToolResults.ReadPaging(arguments);

        // The data sources leave out the data sets the caller may not read, as they do in the designer.
        var entries = await DataSetCatalog.ListAsync(
            services.GetRequiredService<IDataSourceManager>(),
            services.GetRequiredService<IStringLocalizerFactory>(),
            AgentToolAuthorization.GetCaller(services),
            cancellationToken);

        IEnumerable<DataSetCatalog.DataSetEntry> matches = entries;

        if (!string.IsNullOrEmpty(source))
        {
            matches = matches.Where(entry =>
                string.Equals(entry.Source, source, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(entry.SourceName, source, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(query))
        {
            matches = matches.Where(entry =>
                Contains(entry.DataSet.Name, query) ||
                Contains(entry.DataSet.DisplayName, query) ||
                Contains(entry.DataSet.Description, query) ||
                Contains(entry.DataSet.Group, query));
        }

        var list = matches.ToList();

        return AgentToolResults.Json(new
        {
            items = list
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(entry => new
                {
                    value = entry.Value,
                    name = entry.DataSet.Name,
                    displayName = entry.DataSet.DisplayName,
                    description = entry.DataSet.Description,
                    source = entry.Source,
                    sourceName = entry.SourceName,
                    group = entry.DataSet.Group,
                })
                .ToArray(),
            total = list.Count,
            page,
            pageSize,
            truncated = list.Count > page * pageSize,
            sources = entries
                .GroupBy(entry => entry.Source, StringComparer.OrdinalIgnoreCase)
                .Select(group => new { source = group.Key, sourceName = group.First().SourceName })
                .ToArray(),
        });
    }

    private static bool Contains(string value, string text)
        => value?.Contains(text, StringComparison.CurrentCultureIgnoreCase) == true;
}
