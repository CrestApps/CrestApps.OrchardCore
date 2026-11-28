using System.Text.Json;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Omnichannel.Managements.AI;

/// <summary>
/// Deletes an activity load record, as the Delete action of the Load Activities page does, with the page's rules: never
/// while it is loading, and a loaded batch only with Delete loaded activity batches.
/// </summary>
public sealed class DeleteActivityBatchTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteActivityBatch";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The ID of the load to delete, from listActivityBatches."
        }
      },
      "required": ["id"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes an activity load record. Refused while it is loading; a loaded batch also needs the Delete loaded activity batches permission. The activities it already created are kept.";

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

        if (!await AIModuleTools.IsAuthorizedAsync(services, OmnichannelConstants.Permissions.ManageActivityBatches))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var id = AIModuleTools.ReadString(arguments, "id");

        if (id is null)
        {
            return "Nothing was deleted: give the ID of the load in id.";
        }

        var manager = services.GetRequiredService<ICatalogManager<OmnichannelActivityBatch>>();
        var batch = await manager.FindByIdAsync(id, cancellationToken);

        if (batch is null)
        {
            return $"Nothing was deleted: no activity load has the ID '{id}'. Call listActivityBatches to find it.";
        }

        var refusal = await ActivityBatchRules.GetDeleteRefusalAsync(
            services.GetRequiredService<IAuthorizationService>(),
            AIModuleTools.GetCaller(services),
            batch);

        switch (refusal)
        {
            case ActivityBatchDeleteRefusal.Loading:
                return $"Nothing was deleted: the load '{batch.DisplayText}' is being loaded and can't be removed.";

            case ActivityBatchDeleteRefusal.LoadedNotAllowed:
                return $"Nothing was deleted: the load '{batch.DisplayText}' was already loaded, and you are not allowed to remove a loaded batch.";
        }

        if (!await manager.DeleteAsync(batch, cancellationToken))
        {
            return $"Unable to remove the activity load '{batch.DisplayText}'.";
        }

        return AIModuleTools.Json(new
        {
            deleted = new
            {
                id = batch.ItemId,
                displayText = batch.DisplayText,
                status = batch.Status.ToString(),
            },
            note = batch.Status == OmnichannelActivityBatchStatus.Loaded
                ? "The activities it loaded were kept."
                : null,
        });
    }
}
