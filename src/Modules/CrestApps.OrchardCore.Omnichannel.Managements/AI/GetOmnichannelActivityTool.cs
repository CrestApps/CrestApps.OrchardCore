using System.Text.Json;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Omnichannel.Managements.AI;

/// <summary>
/// Returns one CRM activity to a caller who could see it on an admin page: their own, one on a contact whose activities
/// they may list, or any with Manage activities.
/// </summary>
public sealed class GetOmnichannelActivityTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getOmnichannelActivity";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The ID of the activity, from listOmnichannelActivities."
        }
      },
      "required": ["id"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Returns one activity: contact, subject type, channel, schedule, assignment, status, attempts, disposition, notes, hand-off summary and whether an AI session ran it. Times are in the user's time zone.";

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

        if (!await AIModuleTools.IsAuthorizedAsync(services, OmnichannelConstants.Permissions.ListActivities) &&
            !await AIModuleTools.IsAuthorizedAsync(services, OmnichannelConstants.Permissions.ManageActivities))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var id = AIModuleTools.ReadString(arguments, "id");

        if (id is null)
        {
            return "Give the ID of the activity in id. Call listOmnichannelActivities to find it.";
        }

        var activity = await services.GetRequiredService<IOmnichannelActivityManager>().FindByIdAsync(id, cancellationToken);

        // An activity the caller may not see is reported as missing, so its existence is not disclosed.
        if (activity is null || !await ActivityToolSupport.CanReadAsync(services, activity, cancellationToken))
        {
            return $"No activity you can see has the ID '{id}'. Call listOmnichannelActivities to find it.";
        }

        return AIModuleTools.Json(await ActivityToolSupport.DescribeFullAsync(services, activity));
    }
}
