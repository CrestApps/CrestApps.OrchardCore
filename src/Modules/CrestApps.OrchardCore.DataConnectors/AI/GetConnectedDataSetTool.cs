using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.DataConnectors.AI;

/// <summary>
/// Returns one connected data set in full, as its editor and its History page show it.
/// </summary>
public sealed class GetConnectedDataSetTool : DataConnectorAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getConnectedDataSet";

    private const int _maxRuns = 10;

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The ID of the data set, from listConnectedDataSets. Give id or name."
        },
        "name": {
          "type": "string",
          "description": "The technical name of the data set, when the ID is not known."
        },
        "includeRuns": {
          "type": "boolean",
          "description": "Whether to add the latest refreshes (up to 10) with rows read, schema changes and errors. Defaults to false."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Returns one connected data set in full: fields, refresh settings, import state, connector settings, and optionally its latest refresh runs with rows read and errors. Use it before changing a data set or to explain why a refresh failed.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission RequiredPermission => DataConnectorPermissions.ManageConnectedDataSets;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var id = ReadString(arguments, "id");
        var name = ReadString(arguments, "name");

        if (id is null && name is null)
        {
            return "Give the ID of the data set in id, or its technical name in name.";
        }

        if (!TryReadBool(arguments, "includeRuns", out var includeRuns))
        {
            return "includeRuns must be true or false.";
        }

        var store = services.GetRequiredService<IConnectedDataSetStore>();
        var dataSet = id is not null
            ? await store.FindByIdAsync(id, cancellationToken)
            : await store.FindByNameAsync(name, cancellationToken);

        if (dataSet is null)
        {
            return $"No connected data set has the {(id is not null ? "ID" : "name")} '{id ?? name}'. Call listConnectedDataSets to find it.";
        }

        var connection = string.IsNullOrEmpty(dataSet.ConnectionId)
            ? null
            : await services.GetRequiredService<IDataConnectionStore>().FindByIdAsync(dataSet.ConnectionId, cancellationToken);
        var descriptor = connection is null ? null : services.GetRequiredService<IDataConnectorManager>().GetDescriptor(connection.Connector);
        var import = dataSet.Import;
        object runs = null;
        int? runCount = null;

        if (includeRuns == true)
        {
            var (count, page) = await services.GetRequiredService<IDataSetRefreshRunStore>().PageAsync(dataSet.ItemId, 0, _maxRuns, cancellationToken);
            var items = new List<object>();

            foreach (var run in page)
            {
                items.Add(new
                {
                    id = run.ItemId,
                    trigger = run.Trigger.ToString(),
                    mode = run.Mode.ToString(),
                    status = run.Status.ToString(),
                    started = await ToLocalAsync(services, run.StartedUtc),
                    completed = await ToLocalAsync(services, run.CompletedUtc),
                    rowsRead = run.RowsRead,
                    rowCount = run.RowCount,
                    schemaChanges = DataConnectorToolDescriptions.Truncate(run.SchemaChanges, 1_000),
                    error = DataConnectorToolDescriptions.Truncate(run.Error, 1_000),
                    requestedBy = run.RequestedBy,
                });
            }

            runs = items;
            runCount = count;
        }

        return Json(new
        {
            id = dataSet.ItemId,
            name = dataSet.Name,
            displayText = dataSet.DisplayText,
            description = dataSet.Description,
            connection = new
            {
                id = dataSet.ConnectionId,
                displayText = connection?.DisplayText,
                connector = connection?.Connector,
                missing = connection is null,
                connectorEnabled = descriptor is not null,
            },
            resource = dataSet.Resource,
            accessMode = dataSet.AccessMode.ToString(),
            isEnabled = dataSet.IsEnabled,
            readPermission = string.IsNullOrEmpty(dataSet.Name) ? null : DataConnectorPermissions.CreateReadPermission(dataSet.Name).Name,
            fields = (dataSet.Fields ?? []).Select(field => new
            {
                name = field.Name,
                sourceName = field.SourceName,
                displayName = field.DisplayName,
                type = field.Type.ToString(),
                sourceType = field.SourceType.ToString(),
                isIdentifier = field.IsIdentifier,
                isIncluded = field.IsIncluded,
            }).ToArray(),
            defaultDateField = dataSet.DefaultDateField,
            refresh = dataSet.Refresh is null ? null : new
            {
                mode = dataSet.Refresh.Mode.ToString(),
                incrementalField = dataSet.Refresh.IncrementalField,
                intervalMinutes = dataSet.Refresh.IntervalMinutes,
                schemaChangePolicy = dataSet.Refresh.SchemaChangePolicy.ToString(),
            },
            import = import is null ? null : new
            {
                rowCount = import.RowCount,
                hasData = !string.IsNullOrEmpty(import.ActiveVersion),
                lastStatus = import.LastStatus?.ToString(),
                lastError = DataConnectorToolDescriptions.Truncate(import.LastError, 1_000),
                lastRefresh = await ToLocalAsync(services, import.LastRefreshUtc),
                lastSuccess = await ToLocalAsync(services, import.LastSuccessUtc),
                nextRefresh = await ToLocalAsync(services, import.NextRefreshUtc),
                refreshRequested = await ToLocalAsync(services, import.RefreshRequestedUtc),
            },
            connectorSettings = DataConnectorToolDescriptions.DescribeConnectorSettings(dataSet, connection),
            created = await ToLocalAsync(services, dataSet.CreatedUtc),
            modified = await ToLocalAsync(services, dataSet.ModifiedUtc),
            author = dataSet.Author,
            runs,
            runCount,
            editPath = GetAdminPath(services, "ConnectedDataSets", "Edit", dataSet.ItemId),
        });
    }
}
