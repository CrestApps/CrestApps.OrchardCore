using System.Text.Json;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Managements.AI;

/// <summary>
/// Cancels an automated AI call or text that has not started, as the AI activities API does.
/// </summary>
internal sealed class CancelAIActivityTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "cancelAIActivity";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The ID of the automated activity to cancel."
        }
      },
      "required": ["id"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Cancels an automated call or text that has not started yet (status not started, pending or scheduled). Returns a conflict when it is already in progress or finished.";

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

        if (!await AIModuleTools.IsAuthorizedAsync(services, OmnichannelConstants.Permissions.EditActivity))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var id = AIModuleTools.ReadString(arguments, "id");

        if (id is null)
        {
            return "Nothing was cancelled: give the ID of the automated activity.";
        }

        var activityManager = services.GetRequiredService<IOmnichannelActivityManager>();
        var activity = await activityManager.FindByIdAsync(id, cancellationToken);

        // Only an automated AI activity is cancelled here, as in the API; a manual activity is purged or completed instead.
        if (activity is null || string.IsNullOrEmpty(activity.AIProfileId))
        {
            return $"Nothing was cancelled: no automated AI call or text has the ID '{id}'. Call listOmnichannelActivities to find it.";
        }

        if (!await AIActivityCancellation.CancelAsync(activityManager, services.GetRequiredService<IClock>(), activity, cancellationToken))
        {
            return $"Conflict: the {activity.Kind.ToString().ToLowerInvariant()} is {activity.Status} and can no longer be cancelled. A live call is ended with the live call tools.";
        }

        return AIModuleTools.Json(new
        {
            cancelled = true,
            activityId = activity.ItemId,
            kind = activity.Kind.ToString(),
            status = activity.Status.ToString(),
        });
    }
}
