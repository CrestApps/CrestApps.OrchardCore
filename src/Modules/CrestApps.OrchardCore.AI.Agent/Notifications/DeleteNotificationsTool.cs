using System.Text.Json;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Notifications;

namespace CrestApps.OrchardCore.AI.Agent.Notifications;

/// <summary>
/// Removes notifications of the signed-in caller through the notification manager, as the Remove action of the
/// Notifications admin page does. A notification that was sent to another user is never touched.
/// </summary>
public sealed class DeleteNotificationsTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteNotifications";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "notificationIds": {
          "type": "array",
          "minItems": 1,
          "maxItems": 50,
          "description": "The ids of the notifications to remove, from listNotifications.",
          "items": {
            "type": "string"
          }
        }
      },
      "required": [
        "notificationIds"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Permanently removes the given notifications of the calling user; this cannot be undone. It cannot remove the notifications of another user. Suggest markNotifications when the person only wants them out of the unread list.";

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
        var callerId = NotificationToolSupport.GetCallerId(services);

        if (callerId is null || !await AIToolCallerAuthorization.IsAuthorizedAsync(services, NotificationPermissions.ManageNotifications))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var idsError = NotificationToolSupport.ReadNotificationIds(arguments, out var notificationIds);

        if (idsError is not null)
        {
            return $"Nothing was removed: {idsError}";
        }

        if (notificationIds is null)
        {
            return "Nothing was removed: give notificationIds, a list with the id of each notification. Use listNotifications to get the ids.";
        }

        var manager = services.GetRequiredService<INotificationManager>();
        var deleted = new List<string>();
        var notFound = new List<string>();

        foreach (var notificationId in notificationIds)
        {
            var notification = await NotificationToolSupport.FindOwnAsync(manager, notificationId, callerId, cancellationToken);

            if (notification is null)
            {
                notFound.Add(notificationId);

                continue;
            }

            await manager.DeleteAsync(notification, cancellationToken);

            deleted.Add(notificationId);
        }

        return AgentToolResults.Json(new
        {
            deleted,
            notFound = notFound.Count > 0
                ? notFound
                : null,
            note = notFound.Count > 0
                ? "An id that was not found is not a notification of yours. The removed notifications cannot be restored."
                : "The removed notifications cannot be restored.",
        });
    }
}
