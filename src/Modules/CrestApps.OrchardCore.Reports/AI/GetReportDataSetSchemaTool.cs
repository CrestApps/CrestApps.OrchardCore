using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Reports.Designer;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DataSources;
using OrchardCore.DataSources.Expressions;

namespace CrestApps.OrchardCore.Reports.AI;

/// <summary>
/// Returns the fields of one data set and the formula functions, as the Report Builder's designer shows them. It needs
/// the permission that designs reports, and the data source is asked with the caller, so it applies the caller's own
/// read access. It reads the schema only, never a row.
/// </summary>
public sealed class GetReportDataSetSchemaTool : ReportsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getReportDataSetSchema";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "source": {
          "type": "string",
          "description": "The data source's name, from listReportDataSets."
        },
        "dataSet": {
          "type": "string",
          "description": "The data set's name, from listReportDataSets."
        },
        "includeFunctions": {
          "type": "boolean",
          "description": "Also return the formula functions calculated fields can call. True by default."
        }
      },
      "required": ["source", "dataSet"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Returns the fields of one data set with their names, types, groups, descriptions and the data sets they reference, and the formula functions available for calculated fields. Use it before writing a query so the field names are exact.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedAsync(services, user, ReportDesignerPermissions.ManageOwnReportDesigns))
        {
            return NotAllowedMessage;
        }

        var sourceName = ReadString(arguments, "source");
        var dataSetName = ReadString(arguments, "dataSet");

        if (sourceName is null || dataSetName is null)
        {
            return "Give the data source in source and the data set in dataSet. Call listReportDataSets to find them.";
        }

        var dataSource = services.GetRequiredService<IDataSourceManager>().GetDataSource(sourceName);

        if (dataSource is null)
        {
            return $"There is no data source '{sourceName}'. Call listReportDataSets to find it.";
        }

        DataSetSchema schema;

        try
        {
            schema = await dataSource.GetSchemaAsync(dataSetName, new DataSourceContext { User = user }, cancellationToken);
        }
        catch (ReportQueryException exception)
        {
            return string.Join(" ", exception.Errors);
        }

        if (schema is null)
        {
            return $"There is no data set '{dataSetName}' in '{sourceName}' you may read. Call listReportDataSets to find it.";
        }

        object functions = null;

        if (ReadBool(arguments, "includeFunctions") != false)
        {
            functions = (services.GetService<IExpressionFunctionCatalog>()?.Functions ?? [])
                .Select(function => new
                {
                    name = function.Name,
                    category = function.Category,
                    signature = function.Signature,
                    description = function.Description,
                    isAggregate = function.IsAggregate ? true : (bool?)null,
                })
                .ToArray();
        }

        var dataSet = schema.DataSet;

        return Json(new
        {
            source = sourceName,
            dataSet = new
            {
                name = dataSet?.Name ?? dataSetName,
                displayName = dataSet?.DisplayName,
                description = dataSet?.Description,
                group = dataSet?.Group,
                defaultDateField = dataSet?.DefaultDateField,
                references = dataSet?.References?.Select(Describe).ToArray(),
            },
            fields = (schema.Fields ?? []).Where(field => field is not null).Select(field => new
            {
                name = field.Name,
                displayName = field.DisplayName,
                dataType = field.Type.ToString(),
                group = field.Group,
                description = field.Description,
                isIdentifier = field.IsIdentifier ? true : (bool?)null,
                isKeyFilterable = field.IsKeyFilterable ? true : (bool?)null,
                references = field.References is { Count: > 0 } ? field.References.Select(Describe).ToArray() : null,
            }).ToArray(),
            fieldNote = "In a query, refer to a field as alias.FieldName, where alias is the alias you give this data set.",
            functions,
        });
    }

    private static object Describe(DataFieldReference reference)
        => new
        {
            source = reference.Source,
            dataSet = reference.DataSet,
            field = reference.Field,
        };
}
