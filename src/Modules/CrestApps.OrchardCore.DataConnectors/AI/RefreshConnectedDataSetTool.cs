using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.DataConnectors.Models;
using CrestApps.OrchardCore.DataConnectors.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.DataConnectors.AI;

/// <summary>
/// Asks for a refresh of an imported data set, as the Refresh action of the Data Sets page does. The request is stored on
/// the data set and the Connected Data Set Refresh task, which runs every minute, starts it; the admin page also starts
/// it after its request ends, which a chat has no end of request for.
/// </summary>
public sealed class RefreshConnectedDataSetTool : DataConnectorAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "refreshConnectedDataSet";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The ID of the imported data set to refresh, from listConnectedDataSets."
        }
      },
      "required": ["id"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Starts a refresh of an imported data set in the background and returns immediately; it begins within about a minute. Only imported data sets are refreshed; a live one is read from its source every time.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission RequiredPermission => DataConnectorPermissions.RefreshConnectedDataSets;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var id = ReadString(arguments, "id");

        if (id is null)
        {
            return "Nothing was refreshed: give the ID of the data set in id.";
        }

        var dataSet = await services.GetRequiredService<IConnectedDataSetStore>().FindByIdAsync(id, cancellationToken);

        if (dataSet is null)
        {
            return $"Nothing was refreshed: no connected data set has the ID '{id}'. Call listConnectedDataSets to find it.";
        }

        if (dataSet.AccessMode != DataSetAccessMode.Imported)
        {
            return $"Nothing was refreshed: '{dataSet.DisplayText}' is a live data set, which is read from its source every time. Only imported data sets are refreshed.";
        }

        if (!dataSet.IsEnabled)
        {
            return $"Nothing was refreshed: '{dataSet.DisplayText}' is disabled, and the refresh task skips disabled data sets. Enable it first with saveConnectedDataSet.";
        }

        await services.GetRequiredService<DataConnectorCommands>().RequestRefreshAsync(dataSet, cancellationToken);

        return Json(new
        {
            dataSet = new
            {
                id = dataSet.ItemId,
                name = dataSet.Name,
                displayText = dataSet.DisplayText,
            },
            requested = true,
            message = "The refresh was requested and has not run yet: the refresh task starts it within about a minute. Call getConnectedDataSet with includeRuns later to see its result.",
        });
    }
}
