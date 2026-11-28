using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.DataConnectors.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.DataConnectors.AI;

/// <summary>
/// Deletes a connection and its stored credentials, as the Delete action of the Connections page does: it is refused
/// while a data set still reads the connection.
/// </summary>
public sealed class DeleteDataConnectionTool : DataConnectorAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteDataConnection";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The ID of the connection to delete, from listDataConnections."
        }
      },
      "required": ["id"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes a connection and its stored credentials. It is refused while any data set still reads the connection. It cannot be undone.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission RequiredPermission => DataConnectorPermissions.ManageDataConnections;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var id = ReadString(arguments, "id");

        if (id is null)
        {
            return "Nothing was deleted: give the ID of the connection in id.";
        }

        var connection = await services.GetRequiredService<IDataConnectionStore>().FindByIdAsync(id, cancellationToken);

        if (connection is null)
        {
            return $"Nothing was deleted: no data connection has the ID '{id}'. Call listDataConnections to find it.";
        }

        var readers = await services.GetRequiredService<DataConnectorCommands>().DeleteConnectionAsync(connection, cancellationToken);

        if (readers.Count > 0)
        {
            return $"Nothing was deleted: the connection '{connection.DisplayText}' can't be deleted while {readers.Count} data set(s) read it: {string.Join(", ", readers.Select(dataSet => $"{dataSet.DisplayText} ({dataSet.ItemId})"))}. Delete or move those data sets first.";
        }

        return Json(new
        {
            deleted = new
            {
                id = connection.ItemId,
                name = connection.Name,
                displayText = connection.DisplayText,
                connector = connection.Connector,
            },
            message = "The connection and its stored credentials were deleted.",
        });
    }
}
