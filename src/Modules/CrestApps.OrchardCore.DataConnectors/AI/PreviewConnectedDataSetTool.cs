using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.DataConnectors.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.DataSources;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.DataConnectors.AI;

/// <summary>
/// Returns the first rows of a connected data set, read the way reports and data pipelines read it, as the Preview page
/// does: it needs the data set's read permission too, and a disabled data set is not read. The rows are capped at the
/// site's preview size and at 20, and the text of the result at about 40,000 characters.
/// </summary>
public sealed class PreviewConnectedDataSetTool : DataConnectorAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "previewConnectedDataSet";

    /// <summary>
    /// The most rows a preview returns.
    /// </summary>
    public const int MaxRows = 20;

    /// <summary>
    /// The most characters of row values a preview returns.
    /// </summary>
    public const int MaxCharacters = 40_000;

    /// <summary>
    /// The most characters a value keeps.
    /// </summary>
    public const int MaxValueLength = 500;

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The ID of the data set, from listConnectedDataSets."
        },
        "rows": {
          "type": "integer",
          "description": "How many rows to return, from 1 to 20. Defaults to the site's preview size, at most 20."
        }
      },
      "required": ["id"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Returns the first rows of a connected data set with its field names, at most 20 rows. Use it to check that a data set reads what was intended; a disabled data set cannot be read, and the user also needs the data set's read permission.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission RequiredPermission => DataConnectorPermissions.ManageConnectedDataSets;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var id = ReadString(arguments, "id");

        if (id is null)
        {
            return "Give the ID of the data set in id.";
        }

        if (!TryReadInt(arguments, "rows", out var rows))
        {
            return "rows must be a whole number from 1 to 20.";
        }

        var dataSet = await services.GetRequiredService<IConnectedDataSetStore>().FindByIdAsync(id, cancellationToken);

        if (dataSet is null)
        {
            return $"No connected data set has the ID '{id}'. Call listConnectedDataSets to find it.";
        }

        var readPermission = DataConnectorPermissions.CreateReadPermission(dataSet.Name);

        if (!await IsAuthorizedAsync(services, user, readPermission))
        {
            return $"The user can manage the data set '{dataSet.DisplayText}' but not read it. A role needs the permission '{readPermission.Name}' to see its rows.";
        }

        if (!dataSet.IsEnabled)
        {
            return $"The data set '{dataSet.DisplayText}' is disabled, so reports and data pipelines can't read it.";
        }

        var sourceName = dataSet.AccessMode == DataSetAccessMode.Live ? DataConnectorConstants.LiveDataSourceName : DataConnectorConstants.ImportedDataSourceName;
        var dataSource = services.GetRequiredService<IDataSourceManager>().GetDataSource(sourceName);

        if (dataSource is null)
        {
            return $"The data source '{sourceName}' isn't available, so the data set can't be read.";
        }

        var previewRows = services.GetService<IOptions<DataConnectorsOptions>>()?.Value.PreviewRows ?? MaxRows;
        var maxRows = Math.Clamp(rows ?? previewRows, 1, MaxRows);
        DataRowSet result;

        try
        {
            result = await dataSource.ReadRowsAsync(new DataSourceQuery
            {
                DataSet = dataSet.Name,
                Context = new DataSourceContext { User = user },
            }, maxRows, cancellationToken);
        }
        catch (DataConnectorException exception)
        {
            return $"The rows of '{dataSet.DisplayText}' couldn't be read: {exception.Message}";
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            services.GetRequiredService<ILogger<PreviewConnectedDataSetTool>>().LogWarning(exception, "The preview of the data set '{DataSet}' failed.", dataSet.Name);

            return "The rows couldn't be read. See the site's log for details.";
        }

        var fields = result.Fields ?? [];
        var output = new List<JsonArray>();
        var characters = 0;
        var truncated = result.Truncated;
        var valuesCut = false;

        foreach (var row in result.Rows)
        {
            var values = new JsonArray();

            foreach (var value in row ?? [])
            {
                var node = ToValue(value, out var cut);
                valuesCut |= cut;
                values.Add(node);
            }

            var length = values.ToJsonString().Length;

            if (characters + length > MaxCharacters)
            {
                truncated = true;

                break;
            }

            characters += length;
            output.Add(values);
        }

        return Json(new
        {
            dataSet = new
            {
                id = dataSet.ItemId,
                name = dataSet.Name,
                displayText = dataSet.DisplayText,
                accessMode = dataSet.AccessMode.ToString(),
            },
            fields = fields.Select(field => new
            {
                name = field.Name,
                displayName = field.DisplayName,
                type = field.Type.ToString(),
            }).ToArray(),
            rows = output,
            rowCount = output.Count,
            truncated,
            valuesCut,
        });
    }

    private static JsonNode ToValue(object value, out bool cut)
    {
        cut = false;

        switch (value)
        {
            case null:
                return null;

            case bool flag:
                return JsonValue.Create(flag);

            case double real when !double.IsFinite(real):
                return JsonValue.Create(real.ToString(CultureInfo.InvariantCulture));

            case float single when !float.IsFinite(single):
                return JsonValue.Create(single.ToString(CultureInfo.InvariantCulture));

            case int or long or short or byte or decimal or double or float:
                return JsonSerializer.SerializeToNode(value);

            case DateTime dateTime:
                return JsonValue.Create(dateTime.ToString("O", CultureInfo.InvariantCulture));

            case DateTimeOffset dateTimeOffset:
                return JsonValue.Create(dateTimeOffset.ToString("O", CultureInfo.InvariantCulture));

            case JsonNode node:
                return Cut(node.ToJsonString(), out cut);

            case JsonElement element:
                return element.ValueKind == JsonValueKind.String ? Cut(element.GetString(), out cut) : Cut(element.GetRawText(), out cut);

            default:
                return Cut(Convert.ToString(value, CultureInfo.InvariantCulture), out cut);
        }
    }

    private static JsonValue Cut(string text, out bool cut)
    {
        cut = text is not null && text.Length > MaxValueLength;

        return JsonValue.Create(cut ? string.Concat(text.AsSpan(0, MaxValueLength), "...") : text);
    }
}
