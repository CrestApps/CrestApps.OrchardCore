using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Notifications;

namespace CrestApps.OrchardCore.AI.Agent.Notifications;

/// <summary>
/// Marks notifications of the signed-in caller as read or unread through the notification manager, as the
/// Notifications admin page does: the given ones, or every unread one with its "mark all as read" action. A
/// notification that was sent to another user is never touched.
/// </summary>
public sealed class MarkNotificationsTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "markNotifications";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "action": {
          "type": "string",
          "enum": [
            "read",
            "unread"
          ],
          "description": "Mark the notifications as read, or as unread."
        },
        "notificationIds": {
          "type": "array",
          "maxItems": 50,
          "description": "The ids of the notifications to mark, from listNotifications. Give notificationIds or all.",
          "items": {
            "type": "string"
          }
        },
        "all": {
          "type": "boolean",
          "description": "True to mark every notification of the caller as read. It works with the action read only; to mark notifications as unread, give their ids."
        }
      },
      "required": [
        "action"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Marks the calling user's notifications as read or unread, either the given ids or, for read, all of them. It cannot change the notifications of another user.";

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

        if (callerId is null || !await AgentToolAuthorization.IsAuthorizedAsync(services, NotificationPermissions.ManageNotifications))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        arguments.TryGetFirstString("action", out var action);

        action = action?.Trim().ToLowerInvariant();

        if (action is not ("read" or "unread"))
        {
            return "Nothing was changed: action must be read or unread.";
        }

        var idsError = NotificationToolSupport.ReadNotificationIds(arguments, out var notificationIds);

        if (idsError is not null)
        {
            return $"Nothing was changed: {idsError}";
        }

        var all = arguments.GetFirstValueOrDefault("all", false);

        if (all == (notificationIds is not null))
        {
            return "Nothing was changed: give notificationIds, or set all to true, not both. Use listNotifications to get the ids.";
        }

        var manager = services.GetRequiredService<INotificationManager>();
        var markAsRead = action == "read";

        if (all)
        {
            // The admin page has "mark all as read" and nothing that marks everything as unread.
            if (!markAsRead)
            {
                return "Nothing was changed: all works with the action read only. To mark notifications as unread, give their ids.";
            }

            var count = await manager.MarkAsReadAsync(new NotificationFilter
            {
                UserId = callerId,
            }, cancellationToken);

            return AgentToolResults.Json(new
            {
                action,
                all = true,
                changedCount = count,
                note = count == 0
                    ? "You had no unread notifications."
                    : "Every unread notification of yours is marked as read.",
            });
        }

        var changed = new List<string>();
        var unchanged = new List<string>();
        var notFound = new List<string>();

        foreach (var notificationId in notificationIds)
        {
            var notification = await NotificationToolSupport.FindOwnAsync(manager, notificationId, callerId, cancellationToken);

            if (notification is null)
            {
                notFound.Add(notificationId);

                continue;
            }

            var wasChanged = markAsRead
                ? await manager.MarkAsReadAsync(notification, cancellationToken)
                : await manager.MarkAsUnreadAsync(notification, cancellationToken);

            if (wasChanged)
            {
                changed.Add(notificationId);
            }
            else
            {
                unchanged.Add(notificationId);
            }
        }

        return AgentToolResults.Json(new
        {
            action,
            changed,
            alreadyInThatState = unchanged.Count > 0
                ? unchanged
                : null,
            notFound = notFound.Count > 0
                ? notFound
                : null,
            note = notFound.Count > 0
                ? "An id that was not found is not a notification of yours. Use listNotifications to get the ids."
                : null,
        });
    }
}
