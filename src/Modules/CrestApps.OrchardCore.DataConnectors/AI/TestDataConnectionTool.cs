using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.DataConnectors.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.DataConnectors.AI;

/// <summary>
/// Checks that a saved connection reaches its system, as the Test button of the Connections page does, through the
/// same reader, so the site's network rules for data connectors apply.
/// </summary>
public sealed class TestDataConnectionTool : DataConnectorAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "testDataConnection";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The ID of the connection to check, from listDataConnections."
        }
      },
      "required": ["id"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Checks that a saved connection reaches its system and returns success or the error message. Use it after saving a connection or when a data set stops refreshing.";

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
            return "Nothing was checked: give the ID of the connection in id.";
        }

        var connection = await services.GetRequiredService<IDataConnectionStore>().FindByIdAsync(id, cancellationToken);

        if (connection is null)
        {
            return $"Nothing was checked: no data connection has the ID '{id}'. Call listDataConnections to find it.";
        }

        DataConnectorTestResult result;

        try
        {
            result = await services.GetRequiredService<ConnectedDataReader>().TestAsync(connection, cancellationToken)
                ?? DataConnectorTestResult.Failure("The connector gave no answer.");
        }
        catch (DataConnectorException exception)
        {
            result = DataConnectorTestResult.Failure(exception.Message);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            services.GetRequiredService<ILogger<TestDataConnectionTool>>().LogWarning(exception, "The data connection '{Connection}' couldn't be checked.", connection.Name);
            result = DataConnectorTestResult.Failure("The check failed unexpectedly. See the site's log for details.");
        }

        return Json(new
        {
            connection = new
            {
                id = connection.ItemId,
                name = connection.Name,
                displayText = connection.DisplayText,
                connector = connection.Connector,
            },
            succeeded = result.Succeeded,
            message = result.Message,
        });
    }
}
