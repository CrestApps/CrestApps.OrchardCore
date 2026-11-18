using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.DataConnectors.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.DataConnectors.AI;

/// <summary>
/// Deletes a connected data set with its imported rows and its refresh history, as the Delete action of the Data Sets
/// page does.
/// </summary>
public sealed class DeleteConnectedDataSetTool : DataConnectorAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteConnectedDataSet";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The ID of the data set to delete, from listConnectedDataSets."
        }
      },
      "required": ["id"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes a connected data set with its imported rows and its refresh history. Reports and data pipelines that read it will find it missing. It cannot be undone.";

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
            return "Nothing was deleted: give the ID of the data set in id.";
        }

        var dataSet = await services.GetRequiredService<IConnectedDataSetStore>().FindByIdAsync(id, cancellationToken);

        if (dataSet is null)
        {
            return $"Nothing was deleted: no connected data set has the ID '{id}'. Call listConnectedDataSets to find it.";
        }

        await services.GetRequiredService<DataConnectorCommands>().DeleteDataSetAsync(dataSet, cancellationToken);

        return Json(new
        {
            deleted = new
            {
                id = dataSet.ItemId,
                name = dataSet.Name,
                displayText = dataSet.DisplayText,
                accessMode = dataSet.AccessMode.ToString(),
                rowCount = dataSet.Import?.RowCount,
            },
            message = "The data set, its imported rows and its refresh history were deleted. Reports and data pipelines that read it will find it missing.",
        });
    }
}
