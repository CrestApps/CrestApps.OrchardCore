using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.DataConnectors.Models;
using CrestApps.OrchardCore.DataConnectors.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.DataConnectors.AI;

/// <summary>
/// Lists the data connections, as the Connections page does, with their non-secret settings.
/// </summary>
public sealed class ListDataConnectionsTool : DataConnectorAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listDataConnections";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The ID of one connection to return. Leave it out to list connections."
        },
        "search": {
          "type": "string",
          "description": "Text to find in the connection's display name or technical name."
        },
        "page": {
          "type": "integer",
          "description": "The page to return, from 1. Defaults to 1."
        },
        "pageSize": {
          "type": "integer",
          "description": "How many connections a page holds, from 1 to 50. Defaults to 20."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the connections to external systems (HTTP APIs, Elasticsearch, SQL Server, media files) with their connector and non-secret settings, with optional name search or one connection by id. Also returns the connectors enabled on this site. Secret values are never returned, only which secrets are stored, and a header only by its name.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission RequiredPermission => DataConnectorPermissions.ManageDataConnections;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var store = services.GetRequiredService<IDataConnectionStore>();
        var connectorManager = services.GetRequiredService<IDataConnectorManager>();
        var secretManager = services.GetRequiredService<IDataConnectionSecretManager>();
        var connectors = connectorManager.GetConnectors()
            .OrderBy(connector => connector.DisplayName.Value, StringComparer.OrdinalIgnoreCase)
            .Select(DataConnectorToolDescriptions.DescribeConnector)
            .ToArray();

        var id = ReadString(arguments, "id");

        if (id is not null)
        {
            var connection = await store.FindByIdAsync(id, cancellationToken);

            if (connection is null)
            {
                return $"No data connection has the ID '{id}'. Call {TheName} without an id to find it.";
            }

            return Json(new
            {
                connection = await DescribeAsync(services, connectorManager, secretManager, connection),
                connectors,
            });
        }

        var search = ReadString(arguments, "search");
        var (page, pageSize) = ReadPaging(arguments);
        IEnumerable<DataConnection> all = await store.ListAsync(cancellationToken);

        if (search is not null)
        {
            all = all.Where(connection =>
                connection.DisplayText?.Contains(search, StringComparison.OrdinalIgnoreCase) == true ||
                connection.Name?.Contains(search, StringComparison.OrdinalIgnoreCase) == true);
        }

        var matches = all.ToArray();
        var items = new List<object>();

        foreach (var connection in matches.Skip((page - 1) * pageSize).Take(pageSize))
        {
            items.Add(await DescribeAsync(services, connectorManager, secretManager, connection));
        }

        return Json(new
        {
            items,
            total = matches.Length,
            page,
            pageSize,
            truncated = page * pageSize < matches.Length,
            connectors,
        });
    }

    private static async Task<object> DescribeAsync(IServiceProvider services, IDataConnectorManager connectorManager, IDataConnectionSecretManager secretManager, DataConnection connection)
        => DataConnectorToolDescriptions.DescribeConnection(
            connection,
            connectorManager.GetDescriptor(connection.Connector),
            secretManager,
            await ToLocalAsync(services, connection.CreatedUtc),
            await ToLocalAsync(services, connection.ModifiedUtc),
            GetAdminPath(services, "DataConnections", "Edit", connection.ItemId));
}
