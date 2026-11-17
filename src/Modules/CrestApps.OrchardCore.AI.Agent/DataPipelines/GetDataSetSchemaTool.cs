using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.AI.Agent.DataPipelines;

/// <summary>
/// Returns the fields of one data set the caller may read, as the Fields tab of a "Read a data source" step shows them.
/// It reads the schema only, never a row of data. The data set must be one the designer would offer the caller, and the
/// data source is asked with the caller, so it applies the caller's own read access.
/// </summary>
public sealed class GetDataSetSchemaTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getDataSetSchema";

    private const int MaxPageSize = 50;

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "dataSet": {
          "type": "string",
          "description": "The data set as Source::DataSet, such as Contents::BlogPost, from listDataSets."
        },
        "query": {
          "type": "string",
          "description": "Only return the fields whose name, display name or group contains this text."
        },
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page of fields to return, starting at 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "The number of fields per page, 50 by default and at most."
        }
      },
      "required": [
        "dataSet"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Returns the fields of one data set with their names, labels, types and groups, which field identifies a row, which fields can filter by key and what they refer to, and the data set's default date field. It returns no data. Use it before writing the fields, filters and formulas of a pipeline that reads it.";

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

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, OrchardCorePermissions.ManageDataPipelines))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var value = arguments.GetFirstValueOrDefault<string>("dataSet")?.Trim();
        var (sourceName, dataSetName) = DataSetCatalog.Parse(value);

        if (sourceName is null)
        {
            return "Give 'dataSet' as Source::DataSet, such as Contents::BlogPost. Call listDataSets to find it.";
        }

        var dataSourceManager = services.GetRequiredService<IDataSourceManager>();
        var caller = AgentToolAuthorization.GetCaller(services);

        // As the designer does when it adds a data source, only a data set it would offer the caller is read. One answer
        // covers a data set that does not exist and one the caller may not read, so the tool does not tell them apart.
        var entries = await DataSetCatalog.ListAsync(dataSourceManager, services.GetRequiredService<IStringLocalizerFactory>(), caller, cancellationToken);
        var entry = entries.FirstOrDefault(candidate =>
            string.Equals(candidate.Source, sourceName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(candidate.DataSet.Name, dataSetName, StringComparison.Ordinal));

        var schema = entry is null
            ? null
            : await dataSourceManager.GetDataSource(entry.Source).GetSchemaAsync(entry.DataSet.Name, new DataSourceContext { User = caller }, cancellationToken);

        if (schema is null)
        {
            return $"There is no data set '{value}' you may read. Call listDataSets to find the data sets you can use.";
        }

        var query = arguments.GetFirstValueOrDefault<string>("query")?.Trim();
        var page = Math.Max(1, arguments.GetFirstValueOrDefault("page", 1));
        var pageSize = Math.Clamp(arguments.GetFirstValueOrDefault("pageSize", MaxPageSize), 1, MaxPageSize);

        var fields = (schema.Fields ?? []).Where(field => field is not null).ToList();

        if (!string.IsNullOrEmpty(query))
        {
            fields = fields
                .Where(field => Contains(field.Name, query) || Contains(field.DisplayName, query) || Contains(field.Group, query))
                .ToList();
        }

        var dataSet = schema.DataSet ?? entry.DataSet;

        return AgentToolResults.Json(new
        {
            dataSet = entry.Value,
            name = dataSet.Name,
            displayName = dataSet.DisplayName,
            description = dataSet.Description,
            source = entry.Source,
            sourceName = entry.SourceName,
            group = dataSet.Group,
            defaultDateField = dataSet.DefaultDateField,
            references = DescribeReferences(dataSet.References),
            items = fields
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(field => new
                {
                    name = field.Name,
                    displayName = field.DisplayName,
                    type = field.Type.ToString(),
                    group = field.Group,
                    description = field.Description,
                    isIdentifier = field.IsIdentifier,
                    isKeyFilterable = field.IsKeyFilterable,
                    references = DescribeReferences(field.References),
                })
                .ToArray(),
            total = fields.Count,
            page,
            pageSize,
            truncated = fields.Count > page * pageSize,
        });
    }

    private static object[] DescribeReferences(IList<DataFieldReference> references)
    {
        if (references is null || references.Count == 0)
        {
            return null;
        }

        return references
            .Where(reference => reference is not null)
            .Select(reference => (object)new
            {
                dataSet = reference.Source + DataSetCatalog.Separator + reference.DataSet,
                field = reference.Field,
            })
            .ToArray();
    }

    private static bool Contains(string value, string text)
        => value?.Contains(text, StringComparison.CurrentCultureIgnoreCase) == true;
}
