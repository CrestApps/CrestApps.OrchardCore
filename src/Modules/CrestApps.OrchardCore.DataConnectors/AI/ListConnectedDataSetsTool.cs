using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.DataConnectors.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.DataConnectors.AI;

/// <summary>
/// Lists the connected data sets, as the Data Sets page does.
/// </summary>
public sealed class ListConnectedDataSetsTool : DataConnectorAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listConnectedDataSets";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "search": {
          "type": "string",
          "description": "Text to find in the data set's display name or technical name."
        },
        "connectionId": {
          "type": "string",
          "description": "Only the data sets that read this connection."
        },
        "page": {
          "type": "integer",
          "description": "The page to return, from 1. Defaults to 1."
        },
        "pageSize": {
          "type": "integer",
          "description": "How many data sets a page holds, from 1 to 50. Defaults to 20."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the connected data sets with their connection, resource, access mode (live or imported), enabled state, row count, last refresh status and next refresh, with optional name search.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission RequiredPermission => DataConnectorPermissions.ManageConnectedDataSets;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var search = ReadString(arguments, "search");
        var connectionId = ReadString(arguments, "connectionId");
        var (page, pageSize) = ReadPaging(arguments);

        IEnumerable<ConnectedDataSet> all = await services.GetRequiredService<IConnectedDataSetStore>().ListAsync(cancellationToken);

        if (search is not null)
        {
            all = all.Where(dataSet =>
                dataSet.DisplayText?.Contains(search, StringComparison.OrdinalIgnoreCase) == true ||
                dataSet.Name?.Contains(search, StringComparison.OrdinalIgnoreCase) == true);
        }

        if (connectionId is not null)
        {
            all = all.Where(dataSet => string.Equals(dataSet.ConnectionId, connectionId, StringComparison.Ordinal));
        }

        var matches = all.ToArray();
        var connections = (await services.GetRequiredService<IDataConnectionStore>().ListAsync(cancellationToken))
            .ToDictionary(connection => connection.ItemId, StringComparer.Ordinal);
        var items = new List<object>();

        foreach (var dataSet in matches.Skip((page - 1) * pageSize).Take(pageSize))
        {
            items.Add(DataConnectorToolDescriptions.DescribeDataSetRow(
                dataSet,
                connections.GetValueOrDefault(dataSet.ConnectionId ?? string.Empty),
                await ToLocalAsync(services, dataSet.Import?.LastRefreshUtc),
                await ToLocalAsync(services, dataSet.Import?.NextRefreshUtc)));
        }

        return Json(new
        {
            items,
            total = matches.Length,
            page,
            pageSize,
            truncated = page * pageSize < matches.Length,
        });
    }
}
